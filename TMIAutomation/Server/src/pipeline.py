"""Module implementing the inference pipeline."""
import os
import glob
import warnings
import logging
from dataclasses import dataclass
import numpy as np
from rt_utils import RTStructBuilder
from rt_utils.image_helper import get_spacing_between_slices
import imgaug.augmenters as iaa
from imgaug.augmentables import Keypoint, KeypointsOnImage
import matplotlib.pyplot as plt
from scipy import ndimage
from onnxruntime import InferenceSession
from field_geometry_transf import transform_field_geometry, get_zero_row_idx


@dataclass
class RequestInfo:
    """HTTP request information."""

    dicom_path: str
    ptv_name: str
    oars_name: list[str]


@dataclass
class InputImage:
    "Input image - shape (H, W, C) - and properties."
    aspect_ratio: float
    num_slices: int
    width_resize: int
    image: np.ndarray | None


class Pipeline:
    """Pipeline class implementing:
    - preprocessing steps (raw-interim)
    - postprocessing steps (build_output-interim-local_opt-pix_to_pat)
    """

    def __init__(
        self,
        ort_session: InferenceSession,
        input_name: str,
        request_info: RequestInfo,
        save_io: bool = False,
    ):
        self.ort_session = ort_session
        self.input_name = input_name
        self.request_info = request_info
        self.save_io = save_io

        rt_struct_path = glob.glob(
            os.path.join(self.request_info.dicom_path, "RTSTRUCT*")
        )[0]
        self.rtstruct = RTStructBuilder.create_from(
            dicom_series_path=self.request_info.dicom_path,
            rt_struct_path=rt_struct_path,
        )

        pixel_spacing = self.rtstruct.series_data[0].PixelSpacing[0]
        slice_thickness = get_spacing_between_slices(self.rtstruct.series_data)
        aspect_ratio = slice_thickness / pixel_spacing
        num_slices = len(self.rtstruct.series_data)
        self.input_image = InputImage(aspect_ratio, num_slices, 512, None)
        self.model_output = None

    def _get_masked_image_3d(self, mask_3d: np.ndarray) -> np.ndarray:
        """Create a 3D-masked CT image given a 3D mask.

        Args:
            mask_3d (np.ndarray): The 3D mask applied to the 3D CT image.

        Returns:
            np.ndarray: The masked CT image (HU density).
        """
        series_data = self.rtstruct.series_data
        img_shape = list(series_data[0].pixel_array.shape)
        img_shape.append(len(series_data))
        img_3d = np.zeros(img_shape)

        for i, s in enumerate(series_data):
            img_2d = s.pixel_array
            img_3d[..., i] = img_2d

        assert img_3d.shape == mask_3d.shape

        return img_3d * mask_3d

    def _scale_hu_img(
        self, img_2d: np.ndarray, mask_2d: np.ndarray, background: int | None = None
    ) -> np.ndarray:
        """MinMax scale the 2D HU density image.

        Args:
            img_2d (np.ndarray): 2D HU density image.
            mask_2d (np.ndarray): 2D mask.
            background (int, optional): The value to assign to the background pixels.
            If None, the image is MinMax scaled by considering all the pixels. Defaults to None.

        Returns:
            np.ndarray: The MinMax scaled 2D HU density image.
        """
        if background is None:
            min_value = np.min(img_2d)
            pix_intensity_range = np.max(img_2d) - min_value
            scaled_img_2d = (img_2d - min_value) / pix_intensity_range
        else:
            non_zero_values = img_2d[np.nonzero(mask_2d)]
            min_value = np.min(non_zero_values)
            pix_intensity_range = np.max(non_zero_values) - min_value
            scaled_img_2d = np.where(
                mask_2d != 0, (img_2d - min_value) / pix_intensity_range, background
            )

        return scaled_img_2d

    def _transform(self, image: np.ndarray) -> np.ndarray:
        """Transform the image by applying a resize (square image) and rotation (90 degrees CCW).

        Args:
            image (np.ndarray): Original image with shape (H, W, C).

        Returns:
            np.ndarray: The transformed image.
        """
        seq = iaa.Sequential(
            [
                iaa.Resize(
                    size={
                        "height": self.input_image.width_resize,
                        "width": self.input_image.width_resize,
                    },
                    interpolation="nearest",
                ),
                iaa.Rot90(k=-1, keep_size=False),
            ]
        )

        return seq(image=image)

    def preprocess(self) -> np.ndarray:
        """Construct the model's input using the masks of PTV and OARs.

        Returns:
            np.ndarray: The preprocessed image with shape (H, W, C), representing the input of the model. The image
            has three channels (C=3) for, respectively, the 2D HUdensity of the PTV, 2D PTV mask, and 2D OARs mask (overlap).
        """
        ptv_mask_3d = self.rtstruct.get_roi_mask_by_name(
            self.request_info.ptv_name
        )  # axis0=y, axis1=x, axis2=z

        ptv_img_3d = self._get_masked_image_3d(ptv_mask_3d)
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", category=RuntimeWarning)
            ptv_img_2d = ptv_img_3d.mean(  # pylint: disable=unexpected-keyword-arg
                axis=0, where=ptv_img_3d != 0  # coronal projection
            )

        ptv_mask_2d = ptv_mask_3d.any(axis=0)  # coronal projection
        ptv_img_2d = self._scale_hu_img(
            np.nan_to_num(ptv_img_2d), ptv_mask_2d, background=-1
        )

        oars_shape = list(ptv_img_2d.shape)
        oars_shape.append(len(self.request_info.oars_name))
        oars_channel = np.zeros(oars_shape)
        for i, oar_name in enumerate(self.request_info.oars_name):
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
        self.input_image.image = self._transform(image)

        if self.save_io:
            plt.imsave(
                "logs/input_img.png",
                np.where(self.input_image.image == -1, 0, self.input_image.image),
            )

        return self.input_image.image

    def _build_output(self) -> np.ndarray:
        """Build the flat output of the regression.

        Returns:
            np.ndarray: Flat array containing the regression results.
        """
        output = np.zeros(shape=84)
        y_hat = self.model_output[0]

        # Isocenter indexes
        index_x = [0, 3, 6, 9, 12, 15, 18, 21, 24, 27]
        index_y = [1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31, 34]
        output[index_x] = (
            ndimage.center_of_mass(self.input_image.image[0])[0]
            / self.input_image.width_resize
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
            norm = self.input_image.aspect_ratio * self.input_image.num_slices / 512
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

    def _inverse_transform(
        self,
        isocenters_hat: np.ndarray,
        jaws_X_pix_hat: np.ndarray,
        jaws_Y_pix_hat: np.ndarray,
    ) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
        """Transform the model's predictions to the pixel space of the original image,
        by applying scaling, rotation (90 degress CW), and resize to the original image shape.

        Args:
            isocenters_hat (np.ndarray): Isocenter positions in pixel space of the transformed image.
            jaws_X_pix_hat (np.ndarray): Jaw X apertures in pixel space of the transformed image.
            jaws_Y_pix_hat (np.ndarray): Jaw Y apertures in pixel space of the transformed image.

        Returns:
            tuple[np.ndarray, np.ndarray, np.ndarray]: Isocenters, jaw X apertures, and jaw Y apertures
            in pixel space of the original image.
        """
        isocenters_pix = isocenters_hat * self.input_image.width_resize
        jaws_X_pix = jaws_X_pix_hat * self.input_image.width_resize
        jaws_Y_pix = jaws_Y_pix_hat * self.input_image.width_resize

        seq = iaa.Sequential(
            [
                iaa.Rot90(k=1, keep_size=False),
                iaa.Resize(
                    size={
                        "height": self.input_image.width_resize,
                        "width": self.input_image.num_slices,
                    },
                    interpolation="nearest",
                ),
            ]
        )

        # Swap columns to original dicom coordinate system
        isocenters_pix[:, [2, 0]] = isocenters_pix[:, [0, 2]]

        iso_kps = KeypointsOnImage(
            [Keypoint(x=iso[2], y=iso[0]) for iso in isocenters_pix],
            shape=self.input_image.image.shape,
        )

        _, iso_kps_transf = seq(image=self.input_image.image, keypoints=iso_kps)

        iso_kps_tmp = iso_kps_transf.to_xy_array()
        iso_kps_tmp[get_zero_row_idx(isocenters_pix)] = 0

        iso_3d_pix_transf = np.insert(iso_kps_tmp, 1, isocenters_pix[:, 1], axis=1)
        iso_3d_pix_transf[:, [2, 0]] = iso_3d_pix_transf[:, [0, 2]]

        # Only Y apertures need to be resized (X aperture along x/height)
        jaw_Y_pix_transf = (
            jaws_Y_pix * self.input_image.num_slices / self.input_image.width_resize
        )

        return iso_3d_pix_transf, jaws_X_pix, jaw_Y_pix_transf

    def postprocess(self) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
        """Postprocess the model's output.

        Returns:
            tuple[np.ndarray, np.ndarray, np.ndarray]: Isocenters, jaw X apertures, and jaw Y apertures
            in pixel space of the original image.
        """
        output = self._build_output()

        isocenters_hat = np.zeros((12, 3))
        jaws_X_hat = np.zeros((12, 2))
        jaws_Y_hat = np.zeros((12, 2))
        for i in range(12):
            for j in range(3):
                isocenters_hat[i, j] = output[3 * i + j]
                if j < 2:
                    jaws_X_hat[i, j] = output[36 + 2 * i + j]
                    jaws_Y_hat[i, j] = output[60 + 2 * i + j]

        return self._inverse_transform(isocenters_hat, jaws_X_hat, jaws_Y_hat)

    def predict(self) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
        """Execute the entire pipeline to produce predictions from raw data to patient coordinate system.

        Returns:
            tuple[np.ndarray, np.ndarray, np.ndarray]: Isocenters, jaw X apertures, and jaw Y apertures
            in patient coordinate system.
        """
        self.preprocess()

        model_input = np.transpose(
            self.input_image.image[np.newaxis],
            axes=(0, -1, 1, 2),  # swap (H, W, C) --> (C, H, W)
        ).astype(np.float32)

        ort_inputs = {self.input_name: model_input}
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
