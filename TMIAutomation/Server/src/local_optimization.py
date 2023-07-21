"""Module implementing the local optimization
for the abdomen isocenter and related fields."""
import logging
from dataclasses import dataclass, field
from typing import Literal
import numpy as np
from scipy import ndimage
from gradient_free_optimizers import GridSearchOptimizer
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

    # TODO: implement usage of two different models: body and arms
    # For the moment only the body model is used
    def __init__(self, image: Image, field_geometry: FieldGeometry) -> None:
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

        if (
            x_left - (x_right - x_left) / 2
            < self.field_geometry.isocenters_pix[2, 2]
            < x_left + (x_right - x_left) / 2
        ):
            # Shifting the isocenter when it is in the neighborhood above, the jaws are fixed after.
            self.field_geometry.isocenters_pix[2, 2] = x_left - 10
            self.field_geometry.isocenters_pix[3, 2] = x_left - 10

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

        # Set distance between the last two iso only for body model to have symmetric fields
        if self.field_geometry.isocenters_pix[2, 2] < x_left:
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

        ptv_mask = self.image.pixels[..., 1]

        a = (
            self.field_geometry.isocenters_pix[0, 2]
            + self.field_geometry.isocenters_pix[2, 2]
        ) / 2 + 10
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
