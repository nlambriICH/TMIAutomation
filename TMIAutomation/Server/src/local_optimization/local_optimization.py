"""Module implementing the local optimization
for the abdomen isocenter and related fields."""

import logging
from dataclasses import dataclass, field
import numpy as np
from scipy import ndimage
from gradient_free_optimizers import ParallelTemperingOptimizer, GridSearchOptimizer
from src import config
from src.pipeline import Image, FieldGeometry


@dataclass
class OptimizationResult:
    """Result of the local optimization:
    'x' pixels of the maximum extension of the ribs and iliac crests.
    """

    x_pixel_ribs: int = field(default=0)
    x_pixel_iliac: int = field(default=0)


@dataclass
class OptimizationSearchSpace:
    """Search space of the local optimization for iliac crests and ribs
    'x' pixel location.
    """

    x_pixel_left: int = field(default=0)
    x_pixel_right: int = field(default=0)
    y_pixels_right: np.ndarray = field(
        default_factory=lambda: np.zeros(shape=65, dtype=int)
    )
    y_pixels_left: np.ndarray = field(
        default_factory=lambda: np.zeros(shape=65, dtype=int)
    )


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
        self,
        model_name: str,
        image: Image,
        field_geometry: FieldGeometry,
    ) -> None:
        self.model_name = model_name
        self.image = image
        self.field_geometry = field_geometry
        self.optimization_result = OptimizationResult()
        self.optimization_search_space = OptimizationSearchSpace()
        self.field_overlap_pixels = config.YML["field_overlap_pixels"]

    def _fit_collimator_head_field(self):
        ptv_mask = self.image.pixels[..., 1]
        y_pixels = np.arange(
            round(
                self.field_geometry.isocenters_pix[8, 0]
                + self.field_geometry.jaws_Y_pix[9, 0] * self.image.aspect_ratio
            ),
            round(
                self.field_geometry.isocenters_pix[8, 0]
                + self.field_geometry.jaws_Y_pix[9, 1] * self.image.aspect_ratio
            ),
            1,
            dtype=int,
        )
        x_upper_field = round(self.field_geometry.isocenters_pix[8, 2])
        search_space = {
            "x_highest": np.arange(
                x_upper_field,
                ptv_mask.shape[-1],
                1,
                dtype=int,
            )
        }

        def _loss(pos_new):
            # Maximize the ptv field coverage while minimizing the field aperture
            x_highest = pos_new["x_highest"]
            return np.count_nonzero(
                ptv_mask[y_pixels, x_upper_field:x_highest] != 0
            ) - (x_highest - x_upper_field)

        opt = GridSearchOptimizer(search_space)
        opt.search(
            _loss,
            n_iter=search_space["x_highest"].size,
            verbosity=False,
        )

        self.field_geometry.jaws_X_pix[8, 1] = (
            opt.best_value[0] - self.field_geometry.isocenters_pix[8, 2] + 3
        ) * self.image.aspect_ratio

    def _define_search_space(self):
        """Define the optimization search space: 'x' pixel boundary and 'y' pixels range."""
        self.optimization_search_space.x_pixel_left = round(
            (
                self.field_geometry.isocenters_pix[0, 2]
                + self.field_geometry.isocenters_pix[2, 2]
            )
            / 2
        )
        if self.model_name == config.MODEL_NAME_BODY:
            self.optimization_search_space.x_pixel_left += 10
        else:
            self.optimization_search_space.x_pixel_left -= 10

        self.optimization_search_space.x_pixel_right = round(
            (
                self.field_geometry.isocenters_pix[2, 2]
                + self.field_geometry.isocenters_pix[6, 2]
            )
            / 2
        )

        x_com = round(ndimage.center_of_mass(self.image.pixels[..., 0])[0])
        self.optimization_search_space.y_pixels_right = np.arange(
            x_com - 115, x_com - 50
        )
        self.optimization_search_space.y_pixels_left = np.arange(
            x_com + 50, x_com + 115
        )

    def _search_iliac_and_ribs(self):
        """Search the optimal 'x' pixel location of the iliac crests and ribs."""
        ptv_mask = self.image.pixels[..., 1]

        self._define_search_space()

        search_space = {
            "x_iliac": np.arange(
                self.optimization_search_space.x_pixel_left,
                self.optimization_search_space.x_pixel_right,
                1,
                dtype=int,
            ),
            "x_ribs": np.arange(
                self.optimization_search_space.x_pixel_left,
                self.optimization_search_space.x_pixel_right,
                1,
                dtype=int,
            ),
        }

        best_value_ribs = self.image.num_slices
        best_value_iliac = 0
        for y_pixels in (
            self.optimization_search_space.y_pixels_right,
            self.optimization_search_space.y_pixels_left,
        ):

            def _loss(pos_new):
                # Loss:
                # 1) maximize background pixels while minimizing pixels in mask
                # (do not use == 1 to count pixels in mask because the mask is rescaled)
                # 2) maximize the count of background pixels along the 'y' pixels for a
                # given candidate 'x' pixel location

                x_iliac = pos_new["x_iliac"]
                x_ribs = pos_new["x_ribs"]

                # pylint: disable=cell-var-from-loop
                score = 2 * (
                    np.count_nonzero(ptv_mask[y_pixels, x_iliac:x_ribs] == 0)
                    - np.count_nonzero(ptv_mask[y_pixels, x_iliac:x_ribs] != 0)
                ) + 60 * (
                    np.count_nonzero(ptv_mask[y_pixels, x_ribs] == 0)
                    + np.count_nonzero(ptv_mask[y_pixels, x_iliac] == 0)
                )
                # pylint: enable=cell-var-from-loop

                return score

            def _constraint_x_pixel(pos_new):
                return pos_new["x_iliac"] <= pos_new["x_ribs"]

            opt = ParallelTemperingOptimizer(
                search_space, constraints=[_constraint_x_pixel], population=20
            )
            opt.search(
                _loss,
                n_iter=1000,
                verbosity=False,
            )

            if opt.best_value[1] < best_value_ribs:
                best_value_ribs = opt.best_value[1]

            if best_value_iliac < opt.best_value[0]:
                best_value_iliac = opt.best_value[0]

        if best_value_ribs < best_value_iliac:
            logging.warning(
                "Pixel location of ribs < iliac crests. Local optimization might be incorrect."
            )

        self.optimization_result.x_pixel_iliac = best_value_iliac
        self.optimization_result.x_pixel_ribs = best_value_ribs

    def _validate_image(self) -> None:
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

    def _adjust_maximum_distance_iso(self) -> None:
        """
        Ensures the distance between head and pelvis isocenters does not exceed 840 mm.

        Returns:
            None
        """
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

    def _adjust_maximum_distance_arms(self):
        # Fix arms isocenters symmetry
        max_distance = 215 / self.image.pixel_spacing
        x_com = round(ndimage.center_of_mass(self.image.pixels[..., 0])[0])
        left_iso_distance = x_com - self.field_geometry.isocenters_pix[10, 0]
        right_iso_distance = self.field_geometry.isocenters_pix[11, 0] - x_com

        if left_iso_distance > max_distance:
            logging.info(
                "Maximum distance allowed between arms isocenters is %d pixels."
                " Left arm isocenter moved accordingly.",
                max_distance,
            )
            self.field_geometry.isocenters_pix[10, 0] = x_com - max_distance
        if right_iso_distance > max_distance:
            logging.info(
                "Maximum distance allowed between arms isocenters is %d pixels."
                " Right arms isocenter moved accordingly.",
                max_distance,
            )
            self.field_geometry.isocenters_pix[11, 0] = x_com + max_distance

    def _adjust_field_geometry_body(self):
        pass

    def _adjust_field_geometry_arms(self):
        pass
