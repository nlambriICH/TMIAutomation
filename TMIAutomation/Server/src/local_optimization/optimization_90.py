"""Module implementing the local optimization for the abdomen isocenter
and related fields for the models output with 90 deg collimator angle on the pelvis."""

import logging
import numpy as np
from gradient_free_optimizers import GridSearchOptimizer
from src import config
from src.local_optimization.local_optimization import LocalOptimization


class LocalOptimization90(LocalOptimization):
    """Local optimization of the abdomen isocenter and related fields
    for the models output with 90 deg collimator angle on the pelvis."""

    def _adjust_field_geometry_body(self) -> None:
        """Adjust the field geometry predicted by the body model, according to the maximum extension
        of iliac crests and ribs."""

        x_iliac = (
            self.optimization_result.x_pixel_iliac
        )  # iliac crests 'x' pixel location
        x_ribs = self.optimization_result.x_pixel_ribs  # ribs pixel 'x' location

        if (
            x_iliac - (x_ribs - x_iliac)
            < self.field_geometry.isocenters_pix[2, 2]
            < x_iliac + (x_ribs - x_iliac) / 2
        ):
            # Shifting the abdomen and pelvis isocenters toward the feet
            old_pos_abdomen_iso = self.field_geometry.isocenters_pix[2, 2].copy()
            self.field_geometry.isocenters_pix[2, 2] = (
                x_iliac - (x_ribs - x_iliac) / 2.1 - 10
            )
            self.field_geometry.isocenters_pix[3, 2] = (
                x_iliac - (x_ribs - x_iliac) / 2.1 - 10
            )
            self.field_geometry.isocenters_pix[0, 2] -= (
                old_pos_abdomen_iso - self.field_geometry.isocenters_pix[2, 2]
            ) / 2
            self.field_geometry.isocenters_pix[1, 2] -= (
                old_pos_abdomen_iso - self.field_geometry.isocenters_pix[2, 2]
            ) / 2

        # Isocenter on the spine
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
            self.field_geometry.jaws_X_pix[2, 1] = (
                self.field_geometry.isocenters_pix[4, 2]
                - self.field_geometry.isocenters_pix[2, 2]
                + self.field_overlap_pixels
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[5, 0]

        # Set distance between pelvis-abdomen isocenters to have symmetric fields
        if self.field_geometry.isocenters_pix[2, 2] < x_iliac:
            # Thorax isocenters
            self.field_geometry.isocenters_pix[4, 2] = (
                self.field_geometry.isocenters_pix[3, 2]
                + self.field_geometry.isocenters_pix[6, 2]
            ) / 2
            self.field_geometry.isocenters_pix[5, 2] = (
                self.field_geometry.isocenters_pix[3, 2]
                + self.field_geometry.isocenters_pix[6, 2]
            ) / 2

            # Adjust fields of abdomen and thorax iso to the positions of ribs and iliac crests
            self.field_geometry.jaws_X_pix[2, 1] = (
                self.optimization_result.x_pixel_ribs
                - self.field_geometry.isocenters_pix[2, 2]
            ) * self.image.aspect_ratio

            self.field_geometry.jaws_X_pix[5, 0] = (
                self.optimization_result.x_pixel_iliac
                - self.field_geometry.isocenters_pix[4, 2]
            ) * self.image.aspect_ratio

            # Fix overlap of thorax field (upper)
            self.field_geometry.jaws_X_pix[4, 1] = (
                self.field_geometry.isocenters_pix[6, 2]
                - self.field_geometry.isocenters_pix[4, 2]
                + self.field_overlap_pixels
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[7, 0]

            # Field overlap pelvis-abdomen isocenters
            x_midpoint_pelvis_abdomen = (
                self.field_geometry.isocenters_pix[0, 2]
                + self.field_geometry.isocenters_pix[2, 2]
            ) / 2
            self.field_geometry.jaws_X_pix[0, 1] = (
                x_midpoint_pelvis_abdomen
                - self.field_geometry.isocenters_pix[0, 2]
                + self.field_overlap_pixels / 2
            ) * self.image.aspect_ratio
            self.field_geometry.jaws_X_pix[3, 0] = (
                -(
                    x_midpoint_pelvis_abdomen
                    - self.field_geometry.isocenters_pix[0, 2]
                    + self.field_overlap_pixels / 2
                )
                * self.image.aspect_ratio
            )

        self._fit_collimator_pelvic_field()
        self._fit_collimator_head_field()

    def _adjust_field_geometry_arms(self) -> None:
        """Adjust the field geometry predicted by the arms model, according to the maximum extension
        of iliac crests and ribs."""

        x_iliac = (
            self.optimization_result.x_pixel_iliac
        )  # iliac crests 'x' pixel location
        x_ribs = self.optimization_result.x_pixel_ribs  # ribs pixel 'x' location

        if (
            x_ribs - (x_ribs - x_iliac) / 2
            < self.field_geometry.isocenters_pix[2, 2]
            < x_ribs + (x_ribs - x_iliac)
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

        # Isocenter on the spine
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

            # Ensure overlap of abdominal field (lower) and pelvic field (upper), with an overlap specified in config.yml
            self.field_geometry.jaws_X_pix[3, 0] = -(
                (
                    self.field_geometry.isocenters_pix[2, 2]
                    - self.field_geometry.isocenters_pix[0, 2]
                    + self.field_overlap_pixels
                )
                * self.image.aspect_ratio
                - self.field_geometry.jaws_X_pix[0, 1]
            )

            # Ensure overlap of abdominal field (upper) and thorax field (lower), with an overlap specified in config.yml
            self.field_geometry.jaws_X_pix[2, 1] = (
                self.field_geometry.isocenters_pix[6, 2]
                - self.field_geometry.isocenters_pix[2, 2]
                + self.field_overlap_pixels
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[7, 0]

        # Adjust the fields (abdomen/pelvis) according to iliac crests and ribs location
        if self.field_geometry.isocenters_pix[2, 2] > x_ribs:
            self.field_geometry.jaws_X_pix[0, 1] = (
                x_ribs - self.field_geometry.isocenters_pix[0, 2]
            ) * self.image.aspect_ratio
            self.field_geometry.jaws_X_pix[3, 0] = (
                x_iliac - self.field_geometry.isocenters_pix[2, 2]
            ) * self.image.aspect_ratio

        # Fix overlap of abdomen field (upper)
        self.field_geometry.jaws_X_pix[2, 1] = (
            self.field_geometry.isocenters_pix[6, 2]
            - self.field_geometry.isocenters_pix[2, 2]
            + self.field_overlap_pixels
        ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[7, 0]

    def _fit_collimator_pelvic_field(self):
        ptv_mask = self.image.pixels[..., 1]
        y_pixels = np.arange(
            round(
                self.field_geometry.isocenters_pix[0, 0]
                + self.field_geometry.jaws_Y_pix[1, 0] * self.image.aspect_ratio
            ),
            round(
                self.field_geometry.isocenters_pix[0, 0]
                + self.field_geometry.jaws_Y_pix[1, 1] * self.image.aspect_ratio
            ),
            1,
            dtype=int,
        )
        x_lower_field = round(self.field_geometry.isocenters_pix[0, 2])
        search_space = {
            "x_lowest": np.arange(
                0,
                x_lower_field + 1,
                1,
                dtype=int,
            )
        }

        def _loss(pos_new):
            # Maximize the ptv field coverage while minimizing the field aperture
            x_lowest = pos_new["x_lowest"]
            return np.count_nonzero(ptv_mask[y_pixels, x_lowest:x_lower_field] != 0) - (
                x_lower_field - x_lowest
            )

        opt = GridSearchOptimizer(search_space)
        opt.search(
            _loss,
            n_iter=search_space["x_lowest"].size,
            verbosity=False,
        )

        self.field_geometry.jaws_X_pix[1, 0] = (
            opt.best_value[0] - self.field_geometry.isocenters_pix[0, 2] - 3
        ) * self.image.aspect_ratio

    def optimize(self) -> None:
        """Search the 'x' pixel coordinates of the maximum extension
        of the ribs and iliac crests and optimize the abdominal field geometry.
        """
        self._validate_image()
        logging.info("Predicted field geometry: %s", self.field_geometry)

        self._adjust_maximum_distance_iso()
        self._search_iliac_and_ribs()
        logging.info("%s", self.optimization_search_space)
        logging.info("%s", self.optimization_result)

        if self.model_name == config.MODEL_NAME_BODY:
            self._adjust_field_geometry_body()
        elif self.model_name == config.MODEL_NAME_ARMS:
            self._adjust_field_geometry_arms()
            self._adjust_maximum_distance_arms()

        self._fit_collimator_pelvic_field()
        self._fit_collimator_head_field()

        logging.info("Adjusted field geometry: %s", self.field_geometry)
