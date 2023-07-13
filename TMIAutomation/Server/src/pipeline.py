import os
import glob
import warnings
import logging
import numpy as np
from rt_utils import RTStructBuilder
from rt_utils.image_helper import get_spacing_between_slices
import imgaug.augmenters as iaa
from imgaug.augmentables import Keypoint, KeypointsOnImage
import matplotlib.pyplot as plt
from scipy import ndimage
from field_geometry_transf import transform_field_geometry, get_zero_row_idx


class Pipeline:
    def __init__(
        self, ort_session, input_name, dicom_path, ptv_name, oars_name, save_io=False
    ):
        self.ort_session = ort_session
        self.input_name = input_name
        self.dicom_path = dicom_path
        self.ptv_name = ptv_name
        self.oars_name = oars_name
        self.save_io = save_io

        rt_struct_path = glob.glob(os.path.join(dicom_path, "RTSTRUCT*"))[0]
        self.rtstruct = RTStructBuilder.create_from(
            dicom_series_path=dicom_path,
            rt_struct_path=rt_struct_path,
        )

        self.num_slices = len(self.rtstruct.series_data)
        self.width_resize = 512
        self.input_image = None
        self.model_input = None
        self.model_output = None

    def get_masked_image_3d(self, mask_3d):
        series_data = self.rtstruct.series_data
        img_shape = list(series_data[0].pixel_array.shape)
        img_shape.append(len(series_data))
        img_3d = np.zeros(img_shape)

        for i, s in enumerate(series_data):
            img_2d = s.pixel_array
            img_3d[..., i] = img_2d

        assert img_3d.shape == mask_3d.shape

        return img_3d * mask_3d

    def normalize_ptv_img(self, ptv_img_2d, ptv_mask_2d, background=-1):
        non_zero_values = ptv_img_2d[np.nonzero(ptv_mask_2d)]
        min_value = np.min(non_zero_values) if background == -1 else np.min(ptv_img_2d)
        max_value = np.max(non_zero_values) if background == -1 else np.max(ptv_img_2d)
        difference = max_value - min_value
        normalized = (
            np.where(
                ptv_mask_2d != 0, (ptv_img_2d - min_value) / difference, background
            )
            if background == -1
            else (ptv_img_2d - min_value) / difference
        )

        return normalized

    def transform(self, image):
        seq = iaa.Sequential(
            [
                iaa.Resize(
                    size={"height": self.width_resize, "width": self.width_resize},
                    interpolation="nearest",
                ),
                iaa.Rot90(k=-1, keep_size=False),
            ]
        )

        return seq(image=image)

    def preprocess(self):
        ptv_mask_3d = self.rtstruct.get_roi_mask_by_name(
            self.ptv_name
        )  # axis0=y, axis1=x, axis2=z

        ptv_img_3d = self.get_masked_image_3d(ptv_mask_3d)
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", category=RuntimeWarning)
            ptv_img_2d = ptv_img_3d.mean(  # pylint: disable=unexpected-keyword-arg
                axis=0, where=ptv_img_3d != 0  # coronal projection
            )

        ptv_mask_2d = ptv_mask_3d.any(axis=0)  # coronal projection
        ptv_img_2d = self.normalize_ptv_img(np.nan_to_num(ptv_img_2d), ptv_mask_2d)

        oars_shape = list(ptv_img_2d.shape)
        oars_shape.append(len(self.oars_name))
        oars_channel = np.zeros(oars_shape)
        for i, oar_name in enumerate(self.oars_name):
            try:
                oar_mask_2d = self.rtstruct.get_roi_mask_by_name(oar_name).any(axis=0)
            except AttributeError:
                logging.warning(
                    "No contours for %s ROI. Assign mask of zeros.", oar_name
                )
                oar_mask_2d = np.zeros_like(ptv_img_2d)

            if "intestine" not in oar_name:
                logging.info("Scaling %s mask.", oar_name)
                oar_mask_2d = 0.5 * oar_mask_2d

            oars_channel[..., i] = oar_mask_2d

        oars_channel = np.sum(oars_channel, axis=-1)

        image = np.stack((ptv_img_2d, 0.3 * ptv_mask_2d, oars_channel), axis=-1)
        self.input_image = self.transform(image)

        if self.save_io:
            plt.imsave(
                "input_img.png", np.where(self.input_image == -1, 0, self.input_image)
            )

        return self.input_image

    def build_output(self, aspect_ratio):
        output = np.zeros(shape=84)
        y_hat = self.model_output[0]

        # Isocenter indexes
        index_x = [0, 3, 6, 9, 12, 15, 18, 21, 24, 27]
        index_y = [1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31, 34]
        output[index_x] = (
            ndimage.center_of_mass(self.input_image[0])[0] / self.width_resize
        )  # x coord repeated 8 times + 2 times for iso thorax
        output[
            index_y
        ] = 0.5  # y coord repeated 8 times + 2 times for iso thorax, set to 0

        if y_hat.shape[0] == 25:
            output[30] = 0  # x coord right arm
            output[33] = 0  # x coord left arm

            for z in range(2):  # z coords
                output[z * 3 * 2 + 2] = y_hat[z]
                output[z * 3 * 2 + 5] = y_hat[z]
                output[(z + 3) * 3 * 2 + 2] = y_hat[z + 2]
                output[(z + 3) * 3 * 2 + 5] = y_hat[z + 2]
            output[14] = (output[11] + output[20]) / 2
            output[17] = (output[11] + output[20]) / 2
            output[32] = 0  # z coord right arm
            output[35] = 0  # z coord left arm

            # Begin jaw_X
            for i in range(5):
                output[36 + i] = y_hat[4 + i]  # 4 legs + down field 4th iso
            for i in range(3):
                output[42 + i] = y_hat[9 + i]  # 2 4th iso + down field 3rd iso
                output[52 + i] = y_hat[15 + i]  # head fields
                output[56 + i] = 0  # arms fields

            # chest
            output[46] = y_hat[12]  # third iso
            output[48] = y_hat[13]  # chest iso down field
            output[50] = y_hat[14]  # chest iso

            # Overlap fields
            norm = aspect_ratio * self.num_slices / 512
            output[41] = (output[8] - output[14] + 0.01) * norm + output[46]  # abdomen
            output[45] = (output[14] - output[20] + 0.03) * norm + output[50]  # third
            output[49] = (output[20] - output[26] + 0.02) * norm + output[54]  # chest

            # Symmetric apertures
            output[47] = -output[44]  # third iso
            output[51] = -output[48]  # chest
            output[55] = -output[52]  # head
            output[59] = 0  # arms

            # Begin jaw_Y
            for i in range(4):
                if i < 2:
                    # Same apertures opposite signs legs
                    output[60 + 2 * i] = y_hat[i + 18]
                    output[61 + 2 * i] = -y_hat[i + 18]

                    # 4 fields with equal (and opposite) apertures
                    # Pelvis
                    output[64 + 2 * i] = y_hat[20]
                    output[65 + 2 * i] = -y_hat[20]
                    # Third iso
                    output[68 + 2 * i] = y_hat[20]
                    output[69 + 2 * i] = -y_hat[20]
                    # Chest
                    output[72 + 2 * i] = y_hat[20]
                    output[73 + 2 * i] = -y_hat[20]

                    # Arms apertures with opposite sign
                    output[80 + 2 * i] = 0
                    output[81 + 2 * i] = 0

                output[76 + i] = y_hat[21 + i]  # apertures for the head

        return output

    def inverse_transform(self, isocenters_hat, jaws_X_pix_hat, jaws_Y_pix_hat):
        isocenters_pix = isocenters_hat * self.width_resize
        jaws_X_pix = jaws_X_pix_hat * self.width_resize
        jaws_Y_pix = jaws_Y_pix_hat * self.width_resize

        seq = iaa.Sequential(
            [
                iaa.Rot90(k=1, keep_size=False),
                iaa.Resize(
                    size={"height": self.width_resize, "width": self.num_slices},
                    interpolation="nearest",
                ),
            ]
        )

        # Swap columns to original dicom coordinate system
        isocenters_pix[:, [2, 0]] = isocenters_pix[:, [0, 2]]

        iso_kps_img = KeypointsOnImage(
            [Keypoint(x=iso[2], y=iso[0]) for iso in isocenters_pix],
            shape=self.input_image.shape,
        )

        _, iso_kps_img_aug = seq(image=self.input_image, keypoints=iso_kps_img)

        isos_kps_tmp = iso_kps_img_aug.to_xy_array()
        isos_kps_tmp[get_zero_row_idx(isocenters_pix)] = 0

        iso_kps_img_aug_3d = np.insert(isos_kps_tmp, 1, isocenters_pix[:, 1], axis=1)
        iso_kps_img_aug_3d[:, [2, 0]] = iso_kps_img_aug_3d[:, [0, 2]]

        # Only Y apertures need to be resized (X aperture along x/height)
        jaw_Y_pix_aug = jaws_Y_pix * self.num_slices / self.width_resize

        return iso_kps_img_aug_3d, jaws_X_pix, jaw_Y_pix_aug

    def postprocess(self):
        pixel_spacing = self.rtstruct.series_data[0].PixelSpacing[0]
        slice_thickness = get_spacing_between_slices(self.rtstruct.series_data)
        aspect_ratio = slice_thickness / pixel_spacing

        output = self.build_output(aspect_ratio)

        isocenters_hat = np.zeros((12, 3))
        jaws_X_hat = np.zeros((12, 2))
        jaws_Y_hat = np.zeros((12, 2))
        for i in range(12):
            for j in range(3):
                isocenters_hat[i, j] = output[3 * i + j]
                if j < 2:
                    jaws_X_hat[i, j] = output[36 + 2 * i + j]
                    jaws_Y_hat[i, j] = output[60 + 2 * i + j]

        return self.inverse_transform(isocenters_hat, jaws_X_hat, jaws_Y_hat)

    def predict(self):
        self.preprocess()

        self.model_input = np.transpose(
            self.input_image[np.newaxis],
            axes=(0, -1, 1, 2),  # swap (h, w, ch) --> (ch, h, w)
        ).astype(np.float32)

        ort_inputs = {self.input_name: self.model_input}
        ort_outs = self.ort_session.run(None, ort_inputs)  # list of numpy arrays
        self.model_output = ort_outs[0]

        isocenters_pix, jaws_X_pix, jaws_Y_pix = self.postprocess()

        # TODO: local optimization

        return transform_field_geometry(
            self.rtstruct.series_data,
            isocenters_pix,
            jaws_X_pix,
            jaws_Y_pix,
            from_to="pix_pat",
        )
