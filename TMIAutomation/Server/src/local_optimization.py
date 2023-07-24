"""Module implementing the local optimization
for the abdomen isocenter and related fields."""
import logging
from dataclasses import dataclass, field
from typing import Literal
import numpy as np
from scipy import ndimage
from gradient_free_optimizers import GridSearchOptimizer
import config
from pipeline import Image, FieldGeometry


@dataclass
class OptimizationResult:
    """Result of the local optimization:
    'x' pixels of the maximum extension of the ribs and iliac crests.
    """

    min_pos_x_right: int = field(default=0)
    min_pos_x_left: int = field(default=0)


class LocalOptimization:
    """Local optimization of the abdomen isocenter and related fields.
    The algorithm performs roughly the following steps:
    1) Approximate the location of the spine between the iliac crests and ribs.
    2) Within an appropriate neighborhood of this location, search the pixels
    corresponding to the maximum extension of the iliac crests and ribs.
    3) Adjust the abdomen isocenter and related fields according to the iliac crests
    and ribs positions.
    """

    def __init__(
        self, model_name: str, image: Image, field_geometry: FieldGeometry
    ) -> None:
        self.model_name = model_name
        self.image = image
        self.field_geometry = field_geometry
        self.optimization_result = OptimizationResult()

    def _set_spinal_fields(self):
        # TODO: docstring
        self.field_geometry.jaws_X_pix[2, 1] = (
            self.optimization_result.min_pos_x_right
            - self.field_geometry.isocenters_pix[2, 2]
            - 1
        ) * self.image.aspect_ratio

        self.field_geometry.jaws_X_pix[5, 0] = (
            self.optimization_result.min_pos_x_left
            - self.field_geometry.isocenters_pix[4, 2]
            + 1
        ) * self.image.aspect_ratio

    def _adjust_abdominal_fields_geometry(self) -> None:
        """Adjust the abdominal fields geometry according to the maximum extension
        of iliac crests and ribs."""
        x_left = self.optimization_result.min_pos_x_left
        x_right = self.optimization_result.min_pos_x_right

        if self.model_name == config.MODEL_NAME_BODY and (
            x_left - (x_right - x_left) / 2
            < self.field_geometry.isocenters_pix[2, 2]
            < x_left + (x_right - x_left) / 2
        ):
            # Shifting the isocenter when it is in the neighborhood above, the jaws are fixed after
            self.field_geometry.isocenters_pix[2, 2] = x_left - 10
            self.field_geometry.isocenters_pix[3, 2] = x_left - 10
        elif self.model_name == config.MODEL_NAME_ARMS and (
            x_right
            < self.field_geometry.isocenters_pix[2, 2]
            < x_right + (x_right - x_left) / 2
            or x_left > self.field_geometry.isocenters_pix[2, 2]
        ):
            # Setting the isocenters for arms model at 3/4 space
            self.field_geometry.isocenters_pix[2, 2] = (
                x_left + (x_right - x_left) * 3 / 4
            )
            self.field_geometry.isocenters_pix[3, 2] = (
                x_left + (x_right - x_left) * 3 / 4
            )
            # Fixing the fields with minimum overlap, after the isocenter shift
            self.field_geometry.jaws_X_pix[2, 1] = (
                self.field_geometry.isocenters_pix[6, 2]
                - self.field_geometry.isocenters_pix[2, 2]
                + 1
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[3, 0]

        # For both models, if the isocenter is on the spine
        if x_left < self.field_geometry.isocenters_pix[2, 2] < x_right:
            self.field_geometry.jaws_X_pix[2, 0] = (
                (x_left - self.field_geometry.isocenters_pix[2, 2] + 1)
                * self.image.aspect_ratio
                / 2
            )
            self.field_geometry.jaws_X_pix[3, 1] = (
                (x_right - self.field_geometry.isocenters_pix[2, 2] - 1)
                * self.image.aspect_ratio
                / 2
            )

        if (
            self.field_geometry.isocenters_pix[2, 2]
            < self.optimization_result.min_pos_x_left
        ):
            self._set_spinal_fields()

        # Set distance between pelvis-abdomen isos to have symmetric fields
        if config.MODEL_NAME_BODY and self.field_geometry.isocenters_pix[2, 2] < x_left:
            translation = 0.5 * (
                (
                    self.field_geometry.isocenters_pix[2, 2]
                    + self.field_geometry.jaws_X_pix[3, 0] / self.image.aspect_ratio
                )
                - (
                    self.field_geometry.isocenters_pix[0, 2]
                    + self.field_geometry.jaws_X_pix[1, 1] / self.image.aspect_ratio
                )
                + (
                    self.field_geometry.isocenters_pix[4, 2]
                    + self.field_geometry.jaws_X_pix[5, 0] / self.image.aspect_ratio
                )
                - (
                    self.field_geometry.isocenters_pix[2, 2]
                    + self.field_geometry.jaws_X_pix[3, 1] / self.image.aspect_ratio
                )
            )

            # Pelvis isocenters
            self.field_geometry.isocenters_pix[2, 2] = (
                self.field_geometry.isocenters_pix[0, 2]
                + self.field_geometry.jaws_X_pix[1, 1] / self.image.aspect_ratio
                + translation
                - self.field_geometry.jaws_X_pix[3, 0] / self.image.aspect_ratio
            )
            self.field_geometry.isocenters_pix[3, 2] = (
                self.field_geometry.isocenters_pix[0, 2]
                + self.field_geometry.jaws_X_pix[1, 1] / self.image.aspect_ratio
                + translation
                - self.field_geometry.jaws_X_pix[3, 0] / self.image.aspect_ratio
            )

            # Abdomen isocenters
            self.field_geometry.isocenters_pix[4, 2] = (
                self.field_geometry.isocenters_pix[3, 2]
                + self.field_geometry.isocenters_pix[6, 2]
            ) / 2
            self.field_geometry.isocenters_pix[5, 2] = (
                self.field_geometry.isocenters_pix[3, 2]
                + self.field_geometry.isocenters_pix[6, 2]
            ) / 2

            if (
                self.field_geometry.isocenters_pix[2, 2]
                < self.optimization_result.min_pos_x_left
            ):
                self._set_spinal_fields()

        # Move back fields for arms model
        if (
            self.model_name == config.MODEL_NAME_ARMS
            and self.field_geometry.isocenters_pix[2, 2] > x_right
        ):
            self.field_geometry.jaws_X_pix[0, 1] = (
                x_right - self.field_geometry.isocenters_pix[0, 2] - 1
            ) * self.image.aspect_ratio
            self.field_geometry.jaws_X_pix[3, 0] = (
                (x_left - self.field_geometry.isocenters_pix[2, 2]) + 1
            ) * self.image.aspect_ratio

    def _get_pixel_location(
        self,
        x_pixel_spine: int,
        y_pixels: np.ndarray,
        location: Literal["ribs", "iliac"] = "ribs",
    ) -> int:
        """Search the 'x' pixel location of the maximum extension of the iliac crests or ribs.

        Args:
            x_pixel_spine (int): 'x' pixel location of the spine between the maximum extension of iliac crests and ribs.
            y_pixels (np.ndarray): 'y' pixels search space.
            location (Literal["ribs", "iliac"], optional): Which 'x' pixel location to search for,
            either ribs or iliac crests. Defaults to "ribs".

        Returns:
            int: The 'x' pixel location of the maximum extension of the iliac crests or ribs.
        """
        ptv_mask = self.image.pixels[..., 1]
        min_pos_x = x_pixel_spine
        min_pixel_shift = ptv_mask.shape[1]
        if location == "ribs":
            sign = 1
        elif location == "iliac":
            sign = -1
        else:
            logging.warning(
                "Expected location to be 'ribs' or 'iliac' but was '%s'. Defaulting to 'ribs'.",
                location,
            )
            sign = 1

        for j in y_pixels:
            pixel_shift = 0
            for i in range(40):
                if not ptv_mask[j, x_pixel_spine + sign * i]:  # pixel is background
                    pixel_shift += 1
                    if (
                        ptv_mask[j, x_pixel_spine + sign * i]
                        != ptv_mask[j, x_pixel_spine + sign * (i + 1)]
                    ):
                        break
                else:  # pixel in mask
                    pixel_shift = np.inf  # count == np.inf if first pixel is in mask
                    break

            # Assumption: at least one pixel along j is background
            if min_pixel_shift > pixel_shift:
                min_pixel_shift = pixel_shift
                min_pos_x = x_pixel_spine + sign * pixel_shift

        try:
            assert min_pos_x != x_pixel_spine
        except AssertionError:
            logging.exception(
                "Spine position corresponds to ribs/iliac crests position. The local optimization might be incorrect."
            )

        return min_pos_x

    def optimize(self) -> None:
        """Search the 'x' pixel coordinates of the maximum extension
        of the ribs and iliac crests and optimize the abdominal field geometry.
        """
        try:
            assert self.image.pixels.shape == (
                self.image.width_resize,
                self.image.num_slices,
                3,
            )
        except AssertionError:
            logging.exception(
                "Expected original shape image. The local optimization might give incorrect results."
            )

        # Maximum distance between head-pelvis isocenters: 840 mm
        maximum_extension_pix = 840 / self.image.slice_thickness
        head_pelvis_iso_pix_diff = (
            self.field_geometry.isocenters_pix[8, 2]  # head iso-z
            - self.field_geometry.isocenters_pix[0, 2]  # pelvis iso-z
        )
        head_pelvis_iso_dist_pix = abs(head_pelvis_iso_pix_diff)

        if head_pelvis_iso_dist_pix > maximum_extension_pix:
            shift_pixels = np.sign(head_pelvis_iso_pix_diff) * (
                head_pelvis_iso_dist_pix - maximum_extension_pix
            )
            logging.info(
                "Distance between head-pelvis isocenters was %d pixels. Maximum allowed distance is %d (= 84 cm)."
                " Shifting pelvis isocenters by %d pixels.",
                head_pelvis_iso_dist_pix,
                maximum_extension_pix,
                shift_pixels,
            )
            self.field_geometry.isocenters_pix[[0, 1], 2] = (
                self.field_geometry.isocenters_pix[0, 2] + shift_pixels
            )

        ptv_mask = self.image.pixels[..., 1]

        a = (
            self.field_geometry.isocenters_pix[0, 2]
            + self.field_geometry.isocenters_pix[2, 2]
        ) / 2
        if self.model_name == config.MODEL_NAME_BODY:
            a += 10
        b = (
            self.field_geometry.isocenters_pix[2, 2]
            + self.field_geometry.isocenters_pix[6, 2]
        ) / 2
        search_space = {"x_0": np.arange(a, b, 1, dtype=int)}

        def _loss(pos_new):
            x = pos_new["x_0"]
            score = np.sum(ptv_mask[:, x])
            return -score

        opt = GridSearchOptimizer(search_space)
        opt.search(_loss, n_iter=search_space["x_0"].shape[0], verbosity=False)
        best_x_pixel_spine = opt.best_value[0]

        x_com = round(ndimage.center_of_mass(self.image.pixels[..., 0])[0])
        y_pixels = np.concatenate(
            (
                np.arange(x_com - 115, x_com - 50),
                np.arange(x_com + 50, x_com + 115),
            )
        )

        self.optimization_result.min_pos_x_left = self._get_pixel_location(
            best_x_pixel_spine, y_pixels, location="iliac"
        )
        self.optimization_result.min_pos_x_right = self._get_pixel_location(
            best_x_pixel_spine, y_pixels, location="ribs"
        )

        logging.info("%s", self.optimization_result)

        logging.info("Predicted field geometry: %s", self.field_geometry)
        self._adjust_abdominal_fields_geometry()
        logging.info("Adjusted field geometry: %s", self.field_geometry)
