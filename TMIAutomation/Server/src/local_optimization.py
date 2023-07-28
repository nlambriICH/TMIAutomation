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

    x_pixel_ribs: int = field(default=0)
    x_pixel_iliac: int = field(default=0)


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
        self.field_overlap_pixels = config.YML["field_overlap_pixels"]

    def _set_spinal_fields(self):
        """Adjust the X_jaws apertures between the abdomen and thorax isocenters to guarantee an overlap between the two
        fields according to the maximum extension between iliac crests and ribs
        """
        self.field_geometry.jaws_X_pix[2, 1] = (
            self.optimization_result.x_pixel_ribs
            - self.field_geometry.isocenters_pix[2, 2]
            - 1  # shift one pixel from ribs boundary
        ) * self.image.aspect_ratio

        self.field_geometry.jaws_X_pix[5, 0] = (
            self.optimization_result.x_pixel_iliac
            - self.field_geometry.isocenters_pix[4, 2]
            + 1  # shift one pixel from iliac crests boundary
        ) * self.image.aspect_ratio

    def _adjust_abdominal_fields_geometry(self) -> None:
        """Adjust the abdominal fields geometry according to the maximum extension
        of iliac crests and ribs."""

        x_iliac = (
            self.optimization_result.x_pixel_iliac
        )  # iliac crests 'x' pixel location
        x_ribs = self.optimization_result.x_pixel_ribs  # ribs pixel 'x' location

        if self.model_name == config.MODEL_NAME_BODY and (
            x_iliac - (x_ribs - x_iliac) / 2
            < self.field_geometry.isocenters_pix[2, 2]
            < x_iliac + (x_ribs - x_iliac) / 2
        ):
            # Shifting the isocenter when it is in the neighborhood above, the jaws are fixed after
            self.field_geometry.isocenters_pix[2, 2] = x_iliac - 10
            self.field_geometry.isocenters_pix[3, 2] = x_iliac - 10
        elif self.model_name == config.MODEL_NAME_ARMS and (
            x_ribs
            < self.field_geometry.isocenters_pix[2, 2]
            < x_ribs + (x_ribs - x_iliac) / 2
            or self.field_geometry.isocenters_pix[2, 2] < x_iliac
        ):
            # Setting the isocenters for arms model at 3/4 space
            self.field_geometry.isocenters_pix[2, 2] = (
                x_iliac + (x_ribs - x_iliac) * 3 / 4
            )
            self.field_geometry.isocenters_pix[3, 2] = (
                x_iliac + (x_ribs - x_iliac) * 3 / 4
            )
            # Adjust the fields (abdomen/pelvis) after the isocenter shift, with an overlap specified in config.yml
            self.field_geometry.jaws_X_pix[2, 1] = (
                self.field_geometry.isocenters_pix[6, 2]
                - self.field_geometry.isocenters_pix[2, 2]
                + self.field_overlap_pixels
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[7, 0]
            self.field_geometry.jaws_X_pix[0, 1] = (
                self.field_geometry.isocenters_pix[2, 2]
                - self.field_geometry.isocenters_pix[0, 2]
                + self.field_overlap_pixels
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[3, 0]

        # For both models, if the isocenter is on the spine
        if x_iliac < self.field_geometry.isocenters_pix[2, 2] < x_ribs:
            self.field_geometry.jaws_X_pix[2, 0] = (
                (x_iliac - self.field_geometry.isocenters_pix[2, 2])
                * self.image.aspect_ratio
                / 2
            )
            self.field_geometry.jaws_X_pix[3, 1] = (
                (x_ribs - self.field_geometry.isocenters_pix[2, 2])
                * self.image.aspect_ratio
                / 2
            )

            # Increase aperture of abdominal field (upper)
            if self.model_name == config.MODEL_NAME_BODY:
                self.field_geometry.jaws_X_pix[2, 1] = (
                    self.field_geometry.isocenters_pix[4, 2]
                    - self.field_geometry.isocenters_pix[2, 2]
                    + self.field_overlap_pixels
                ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[5, 0]

        # Set distance between pelvis-abdomen isocenters to have symmetric fields
        if (
            self.model_name == config.MODEL_NAME_BODY
            and self.field_geometry.isocenters_pix[2, 2] < x_iliac
        ):
            self._set_spinal_fields()

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

            # Pelvic isocenters
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

            if self.field_geometry.isocenters_pix[2, 2] < x_iliac:
                self._set_spinal_fields()

        # Adjust the fields (abdomen/pelvis) according to iliac crests and ribs location
        if (
            self.model_name == config.MODEL_NAME_ARMS
            and self.field_geometry.isocenters_pix[2, 2] > x_ribs
        ):
            self.field_geometry.jaws_X_pix[0, 1] = (
                x_ribs
                - self.field_geometry.isocenters_pix[0, 2]
                - 1  # shift one pixel from ribs boundary
            ) * self.image.aspect_ratio
            self.field_geometry.jaws_X_pix[3, 0] = (
                x_iliac
                - self.field_geometry.isocenters_pix[2, 2]
                + 1  # shift one pixel from iliac crests boundary
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
                    if (  # next pixel is in mask
                        ptv_mask[j, x_pixel_spine + sign * i]
                        != ptv_mask[j, x_pixel_spine + sign * (i + 1)]
                    ):
                        for k in range(1, 10):
                            if not ptv_mask[j, x_pixel_spine + sign * (i + 1 + k)]:
                                # pixel_shift == np.inf if background is found after a few pixels (i.e., spine)
                                pixel_shift = np.inf
                        break
                else:  # pixel in mask
                    # pixel_shift == np.inf if first pixel is in mask
                    pixel_shift = np.inf
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

    def _search_x_pixel_spine(self) -> int:
        """Search the optimal 'x' pixel location of the spine with GridSearch.

        Returns:
            int: The optimal 'x' pixel location of the spine.
        """
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
        return opt.best_value[0]

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

        best_x_pixel_spine = self._search_x_pixel_spine()
        x_com = round(ndimage.center_of_mass(self.image.pixels[..., 0])[0])
        y_pixels_right = np.arange(x_com - 115, x_com - 50)
        y_pixels_left = np.arange(x_com + 50, x_com + 115)
        y_pixels = np.concatenate(
            (
                y_pixels_right,
                y_pixels_left,
            )
        )

        self.optimization_result.x_pixel_iliac = self._get_pixel_location(
            best_x_pixel_spine, y_pixels, location="iliac"
        )
        self.optimization_result.x_pixel_ribs = self._get_pixel_location(
            best_x_pixel_spine, y_pixels, location="ribs"
        )

        logging.info("%s", self.optimization_result)

        logging.info("Predicted field geometry: %s", self.field_geometry)
        self._adjust_abdominal_fields_geometry()
        logging.info("Adjusted field geometry: %s", self.field_geometry)

        if not config.BUNDLED:
            from visualize import (  # pylint: disable=import-outside-toplevel
                save_local_opt,
            )

            save_local_opt(
                self.image,
                best_x_pixel_spine,
                y_pixels_right,
                y_pixels_left,
                self.optimization_result.x_pixel_ribs,
                self.optimization_result.x_pixel_iliac,
            )
