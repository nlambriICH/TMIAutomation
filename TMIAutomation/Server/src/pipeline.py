"""Module implementing the inference pipeline."""
import os
import glob
import warnings
import logging
from dataclasses import dataclass, field
import numpy as np
from thefuzz import fuzz
from rt_utils import RTStructBuilder
from rt_utils.image_helper import get_spacing_between_slices
import imgaug.augmenters as iaa
from imgaug.augmentables import Keypoint, KeypointsOnImage
from scipy import ndimage
from flask import abort
import config
from field_geometry_transf import (
    transform_field_geometry,
    get_zero_row_idx,
    adjust_to_max_aperture,
)


@dataclass
class RequestInfo:
    """HTTP request information."""

    model_name: str
    dicom_path: str
    ptv_name: str
    oars_name: list[str]


@dataclass
class Image:
    "Image - shape (H, W, C) - processed by the pipeline, and its properties."

    pixel_spacing: float
    slice_thickness: float
    aspect_ratio: float
    num_slices: int
    width_resize: int
    pixels: np.ndarray | None = field(default=None)


@dataclass
class FieldGeometry:
    "Field geometry (isocenter positions and jaw apertures) in pixel coordinate system."

    isocenters_pix: np.ndarray = field(default_factory=lambda: np.zeros(shape=(12, 3)))
    jaws_X_pix: np.ndarray = field(default_factory=lambda: np.zeros(shape=(12, 2)))
    jaws_Y_pix: np.ndarray = field(default_factory=lambda: np.zeros(shape=(12, 2)))


class Pipeline:
    """Pipeline class implementing:
    1) Preprocessing steps (raw-interim)
    2) Postprocessing steps (build_output-interim-local_opt-pix_to_pat)
    """

    def __init__(
        self,
        request_info: RequestInfo,
    ) -> None:
        self.request_info = request_info
        self.patient_id = os.path.basename(self.request_info.dicom_path)

        rt_struct_path = []
        for root in ("RTSTRUCT*", "RS*"):
            rt_struct_path.extend(
                glob.glob(os.path.join(self.request_info.dicom_path, root))
            )
        self.rtstruct = RTStructBuilder.create_from(
            dicom_series_path=self.request_info.dicom_path,
            rt_struct_path=rt_struct_path[0],
        )

        pixel_spacing = self.rtstruct.series_data[0].PixelSpacing[0]
        slice_thickness = get_spacing_between_slices(self.rtstruct.series_data)
        aspect_ratio = slice_thickness / pixel_spacing
        num_slices = len(self.rtstruct.series_data)
        self.image = Image(
            pixel_spacing, slice_thickness, aspect_ratio, num_slices, width_resize=512
        )
        self.field_geometry = FieldGeometry()

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
                        "height": self.image.width_resize,
                        "width": self.image.width_resize,
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
        if config.BUNDLED:
            ptv_mask_3d = self.rtstruct.get_roi_mask_by_name(
                self.request_info.ptv_name
            )  # axis0=y, axis1=x, axis2=z
        else:
            ptv_mask_3d_ = self.rtstruct.get_roi_mask_by_name(
                self.request_info.ptv_name[0]
            )  # axis0=y, axis1=x, axis2=z

            junction_mask_3d = np.zeros_like(ptv_mask_3d_)
            for junc in self.request_info.ptv_name[1]:
                junction_mask_3d |= self.rtstruct.get_roi_mask_by_name(
                    junc
                )  # axis0=y, axis1=x, axis2=z

            ptv_mask_3d = ptv_mask_3d_ | junction_mask_3d

        ptv_img_3d = self._get_masked_image_3d(ptv_mask_3d)
        with warnings.catch_warnings():
            warnings.simplefilter("ignore", category=RuntimeWarning)
            ptv_img_2d = ptv_img_3d.mean(  # pylint: disable=unexpected-keyword-arg
                axis=0, where=ptv_img_3d != 0  # coronal projection
            )

        ptv_mask_2d = ptv_mask_3d.any(axis=0)  # coronal projection
        ptv_img_2d = self._scale_hu_img(
            np.nan_to_num(ptv_img_2d), ptv_mask_2d, background=0
        )

        # Words and similarity threshold for intestine mask scaling
        target_words, threshold = [
            "intestino",
            "bowel",
        ], 80
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

            similarities = [
                fuzz.ratio(oar_name.lower(), target) for target in target_words
            ]
            if not any(similarity >= threshold for similarity in similarities):
                logging.info("Scaling mask %s.", oar_name)
                oar_mask_2d = 0.5 * oar_mask_2d

            oars_channel[..., i] = oar_mask_2d

        oars_channel = oars_channel.max(axis=-1)

        image = np.stack((ptv_img_2d, 0.3 * ptv_mask_2d, oars_channel), axis=-1)
        self.image.pixels = self._transform(image)

        if not config.BUNDLED:
            from visualize import (  # pylint: disable=import-outside-toplevel
                save_input_img,
            )

            save_input_img(self.patient_id, self.image)

    def _build_output(self, model_output: np.ndarray) -> np.ndarray:
        """Build the flat output of the regression.

        Args:
            model_output (np.ndarray): Output of regression model.

        Returns:
            np.ndarray: Flat array containing the regression results.
        """
        output = np.zeros(shape=84)
        y_hat = model_output[0]

        # Isocenter indexes
        index_x = [0, 3, 6, 9, 12, 15, 18, 21, 24, 27]
        index_y = [1, 4, 7, 10, 13, 16, 19, 22, 25, 28, 31, 34]
        norm = self.image.aspect_ratio * self.image.num_slices / self.image.width_resize

        try:
            assert self.image.pixels.shape == (
                self.image.width_resize,
                self.image.width_resize,
                3,
            )
        except AssertionError:
            logging.exception(
                "Expected square image. The reconstructed output might be incorrect."
            )

        output[index_x] = (
            ndimage.center_of_mass(self.image.pixels[..., 0])[1]
            / self.image.width_resize
        )  # x coord repeated 8 times + 2 times for iso thorax
        output[
            index_y
        ] = 0.5  # y coord repeated 8 times + 2 times for iso thorax, set to 0

        if y_hat.shape[0] == 25:
            self._build_body_cnn_output(output, y_hat, norm)

        elif y_hat.shape[0] == 30:
            self._build_arms_cnn_output(output, y_hat, norm)

        return output

    def _build_arms_cnn_output(
        self, output: np.ndarray, y_hat: np.ndarray, norm: float
    ) -> None:
        """Build the output of the arms_cnn model.

        Args:
            output (np.ndarray): Array of shape=(84,) containing the (x, y) coordinates of the isocenters.
            y_hat (np.ndarray): Model predictions.
            norm (float): Normalization factor used to scale the field coordinates to compute the
            overlap of fields along X.
        """
        output[30] = y_hat[0]  # x coord right arm
        output[33] = y_hat[1]  # x coord left arm

        for z in range(2):  # first two z coords
            output[z * 3 * 2 + 2] = y_hat[z + 2]
            output[z * 3 * 2 + 5] = y_hat[z + 2]
        output[20] = 0  # we skip the third iso
        output[23] = 0  # we skip the third iso
        for z in range(3):  # last three z coords we skip the third iso
            output[(z + 3) * 3 * 2 + 2] = y_hat[z + 4]
            output[(z + 3) * 3 * 2 + 5] = y_hat[z + 4]

        # Begin jaw_X
        # 4 legs + 3 pelvis
        for i in range(5):
            output[36 + i] = y_hat[7 + i]  # retrieve apertures of first 11 fields
        output[42] = y_hat[12]
        output[43] = y_hat[13]
        # 3 for third iso = null + one symmetric (thus 0)
        for i in range(3):
            output[44 + i] = 0
        # 3 for chest iso = null + one symmetric (again 0)
        output[48] = y_hat[14]
        output[50] = y_hat[15]  # add in groups of three avoiding repetitions

        for i in range(3):
            output[52 + i] = y_hat[16 + i]  # head
            output[56 + i] = y_hat[19 + i]  # arms

        # Symmetric apertures
        output[51] = -output[48]
        output[55] = -output[52]

        output[59] = y_hat[22]
        # Overlap fields
        output[41] = (y_hat[3] - y_hat[4] + 0.01) * norm + output[50]  # abdomen
        output[49] = (y_hat[4] - y_hat[5] + 0.03) * norm + output[54]  # chest

        # Begin jaw_Y
        for i in range(4):
            output[76 + i] = y_hat[26 + i]  # apertures for the head
            if i < 2:
                # Same apertures opposite signs #LEGS
                output[60 + 2 * i] = y_hat[i + 23]
                output[61 + 2 * i] = -y_hat[i + 23]

                # 4 fields with equal (and opposite) apertures
                output[64 + 2 * i] = y_hat[24]
                output[65 + 2 * i] = -y_hat[24]
                output[68 + 2 * i] = 0  # index 35 == thorax iso
                output[69 + 2 * i] = 0

                # 2 fields with equal (and opposite) apertures
                output[72 + 2 * i] = y_hat[24]
                output[73 + 2 * i] = -y_hat[24]

                # Arms apertures with opposite sign
                scaling_factor_mm_norm = (
                    self.image.slice_thickness * self.image.num_slices
                )  # distance [mm] / scaling_factor to compute normalized aperture in pixel space
                output[80 + 2 * i] = -200 / scaling_factor_mm_norm
                output[81 + 2 * i] = 200 / scaling_factor_mm_norm

    def _build_body_cnn_output(
        self, output: np.ndarray, y_hat: np.ndarray, norm: float
    ) -> None:
        """Build the output of the body_cnn model.

        Args:
            output (np.ndarray): Array of shape=(84,) containing the (x, y) coordinates of the isocenters.
            y_hat (np.ndarray): Model predictions.
            norm (float): Normalization factor used to scale the field coordinates to compute the
            overlap of fields along X.
        """
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
            output[76 + i] = y_hat[21 + i]  # apertures for the head
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

    def _inverse_transform(
        self,
        isocenters_hat: np.ndarray,
        jaws_X_pix_hat: np.ndarray,
        jaws_Y_pix_hat: np.ndarray,
    ) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
        """Transform the model's predictions to the pixel space of the original image,
        by applying scaling, rotation (90 degrees CW), and resize to the original image shape.

        Args:
            isocenters_hat (np.ndarray): Isocenter positions in pixel space of the transformed image.
            jaws_X_pix_hat (np.ndarray): Jaw X apertures in pixel space of the transformed image.
            jaws_Y_pix_hat (np.ndarray): Jaw Y apertures in pixel space of the transformed image.

        Returns:
            tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray]: Original image, isocenters,
            jaw X apertures, and jaw Y apertures in pixel space of the original image.
        """
        isocenters_pix = isocenters_hat * self.image.width_resize
        jaws_X_pix = jaws_X_pix_hat * self.image.width_resize
        jaws_Y_pix = jaws_Y_pix_hat * self.image.width_resize

        seq = iaa.Sequential(
            [
                iaa.Rot90(k=1, keep_size=False),
                iaa.Resize(
                    size={
                        "height": self.image.width_resize,
                        "width": self.image.num_slices,
                    },
                    interpolation="nearest",
                ),
            ]
        )

        # Swap columns to original dicom coordinate system
        isocenters_pix[:, [2, 0]] = isocenters_pix[:, [0, 2]]

        iso_kps = KeypointsOnImage(
            [Keypoint(x=iso[2], y=iso[0]) for iso in isocenters_pix],
            shape=self.image.pixels.shape,
        )

        image_original, iso_kps_transf = seq(image=self.image.pixels, keypoints=iso_kps)

        iso_kps_tmp = iso_kps_transf.to_xy_array()
        iso_kps_tmp[get_zero_row_idx(isocenters_pix)] = 0

        iso_3d_pix_transf = np.insert(iso_kps_tmp, 1, isocenters_pix[:, 1], axis=1)
        iso_3d_pix_transf[:, [2, 0]] = iso_3d_pix_transf[:, [0, 2]]

        # Only Y apertures need to be resized (X aperture along x/height)
        jaw_Y_pix_transf = jaws_Y_pix * self.image.num_slices / self.image.width_resize

        return image_original, iso_3d_pix_transf, jaws_X_pix, jaw_Y_pix_transf

    def postprocess(self, model_output: np.ndarray) -> None:
        """Postprocess the model's output.

        Args:
            model_output (np.ndarray): Output of regression model.
        """
        output = self._build_output(model_output)

        isocenters_hat = np.zeros((12, 3))
        jaws_X_hat = np.zeros((12, 2))
        jaws_Y_hat = np.zeros((12, 2))
        for i in range(12):
            for j in range(3):
                isocenters_hat[i, j] = output[3 * i + j]
                if j < 2:
                    jaws_X_hat[i, j] = output[36 + 2 * i + j]
                    jaws_Y_hat[i, j] = output[60 + 2 * i + j]

        (
            self.image.pixels,
            self.field_geometry.isocenters_pix,
            self.field_geometry.jaws_X_pix,
            self.field_geometry.jaws_Y_pix,
        ) = self._inverse_transform(isocenters_hat, jaws_X_hat, jaws_Y_hat)

    def predict(
        self, local_opt: bool = True
    ) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
        """Execute the entire pipeline to produce predictions from raw data to patient coordinate system.

        Args:
            local_opt (bool): Whether to perform the local optimization of the model's output
            for the abdominal field geometry. Defaults to True.

        Returns:
            tuple[np.ndarray, np.ndarray, np.ndarray]: Isocenters, jaw X apertures, and jaw Y apertures
            in patient coordinate system.
        """
        if (
            self.request_info.model_name == config.MODEL_NAME_BODY
            and config.ORT_SESSION_BODY is not None
        ):
            ort_session = config.ORT_SESSION_BODY
        elif (
            self.request_info.model_name == config.MODEL_NAME_ARMS
            and config.ORT_SESSION_ARMS is not None
        ):
            ort_session = config.ORT_SESSION_ARMS
        else:
            abort(503)

        self.preprocess()

        model_input = np.transpose(
            self.image.pixels[np.newaxis],
            axes=(0, -1, 1, 2),  # swap (H, W, C) --> (C, H, W)
        ).astype(np.float32)

        input_name = ort_session.get_inputs()[0].name
        ort_inputs = {input_name: model_input}
        ort_outs = ort_session.run(None, ort_inputs)  # list of numpy arrays
        model_output = ort_outs[0]

        self.postprocess(model_output)

        if local_opt:
            from local_optimization import (  # pylint: disable=import-outside-toplevel
                LocalOptimization,
            )

            local_optimization = LocalOptimization(
                self.request_info.model_name,
                self.image,
                self.field_geometry,
            )
            local_optimization.optimize()

            if not config.BUNDLED:
                from visualize import (  # pylint: disable=import-outside-toplevel
                    save_local_opt,
                )

                save_local_opt(self.patient_id, self.image, local_optimization)

        if not config.BUNDLED:
            from visualize import (  # pylint: disable=import-outside-toplevel
                save_field_geometry,
            )

            save_field_geometry(
                self.patient_id,
                self.request_info.model_name,
                self.image,
                self.field_geometry,
            )

        (
            isocenters_pat_coord,
            jaws_X_pat_coord,
            jaws_Y_pat_coord,
        ) = transform_field_geometry(
            self.rtstruct.series_data,
            self.field_geometry.isocenters_pix,
            self.field_geometry.jaws_X_pix,
            self.field_geometry.jaws_Y_pix,
            from_to="pix_pat",
        )

        return (
            isocenters_pat_coord,
            adjust_to_max_aperture(jaws_X_pat_coord),
            adjust_to_max_aperture(jaws_Y_pat_coord, 175),
        )
