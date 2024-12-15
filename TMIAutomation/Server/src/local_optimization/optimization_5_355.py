"""Module implementing the local optimization for the abdomen isocenter
and related fields for the models output with 5/355 deg collimator angle on the pelvis."""

import logging
from src import config
from src.local_optimization.local_optimization import LocalOptimization


class LocalOptimization5355(LocalOptimization):
    """Local optimization of the abdomen isocenter and related fields
    for the models output with 5/355 deg collimator angle on the pelvis."""

    def _adjust_field_geometry_body(self):
        """Adjust the field geometry predicted by the body model, according to the maximum extension
        of iliac crests and ribs."""

        x_iliac = (
            self.optimization_result.x_pixel_iliac
        )  # iliac crests 'x' pixel location
        x_ribs = self.optimization_result.x_pixel_ribs  # ribs pixel 'x' location

        self.field_geometry.isocenters_pix[2, 2] = (x_iliac + x_ribs) / 2
        self.field_geometry.isocenters_pix[3, 2] = (x_iliac + x_ribs) / 2

        # Fix aperture of abdomen fields
        if x_ribs - x_iliac < self.field_overlap_pixels:
            self.field_geometry.jaws_X_pix[2, 0] = (
                x_iliac - self.field_geometry.isocenters_pix[2, 2]
            ) * self.image.aspect_ratio
            self.field_geometry.jaws_X_pix[3, 1] = (
                x_ribs - self.field_geometry.isocenters_pix[2, 2]
            ) * self.image.aspect_ratio
        else:
            self.field_geometry.jaws_X_pix[2, 0] = (
                -self.field_overlap_pixels * self.image.aspect_ratio / 2
            )
            self.field_geometry.jaws_X_pix[3, 1] = (
                self.field_overlap_pixels * self.image.aspect_ratio / 2
            )

        # Fix thorax isocenter
        self.field_geometry.isocenters_pix[4, 2] = (
            self.field_geometry.isocenters_pix[2, 2]
            + self.field_geometry.isocenters_pix[6, 2]
        ) / 2
        self.field_geometry.isocenters_pix[5, 2] = (
            self.field_geometry.isocenters_pix[2, 2]
            + self.field_geometry.isocenters_pix[6, 2]
        ) / 2

        # Increase aperture of abdominal field (upper)
        self.field_geometry.jaws_X_pix[2, 1] = (
            self.field_geometry.isocenters_pix[4, 2]
            - self.field_geometry.isocenters_pix[2, 2]
            + self.field_overlap_pixels
        ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[5, 0]

        # Fix overlap between abdomen and thorax
        self.field_geometry.jaws_X_pix[5, 0] = (
            (
                self.field_geometry.isocenters_pix[2, 2]
                - self.field_geometry.isocenters_pix[4, 2]
                - self.field_overlap_pixels
            )
            * self.image.aspect_ratio
            / 2
        )
        self.field_geometry.jaws_X_pix[2, 1] = (
            (
                self.field_geometry.isocenters_pix[4, 2]
                - self.field_geometry.isocenters_pix[2, 2]
                + self.field_overlap_pixels
            )
            * self.image.aspect_ratio
            / 2
        )

        # Fix overlap of thorax field (upper)
        self.field_geometry.jaws_X_pix[4, 1] = (
            (
                self.field_geometry.isocenters_pix[6, 2]
                - self.field_geometry.isocenters_pix[4, 2]
                + self.field_overlap_pixels
            )
            * self.image.aspect_ratio
            / 2
        )

        self.field_geometry.jaws_X_pix[7, 0] = (
            (
                self.field_geometry.isocenters_pix[4, 2]
                - self.field_geometry.isocenters_pix[6, 2]
                - self.field_overlap_pixels
            )
            * self.image.aspect_ratio
            / 2
        )

        # Fix overlap of pelvis and abdomen field
        min_aperture = (
            self.field_geometry.isocenters_pix[0, 2]
            - self.field_geometry.isocenters_pix[2, 2]
            + self.field_geometry.jaws_Y_pix[0, 1]
            - self.field_overlap_pixels
        ) * self.image.aspect_ratio
        if abs(self.field_geometry.jaws_X_pix[3, 0]) < abs(min_aperture):
            self.field_geometry.jaws_X_pix[3, 0] = min_aperture

    def _adjust_field_geometry_arms(self) -> None:
        x_iliac = (
            self.optimization_result.x_pixel_iliac
        )  # iliac crests 'x' pixel location
        x_ribs = self.optimization_result.x_pixel_ribs  # ribs pixel 'x' location

        if (
            x_ribs - (x_ribs - x_iliac) / 2
            < self.field_geometry.isocenters_pix[2, 2]
            < x_ribs + (x_ribs - x_iliac)
            or self.field_geometry.isocenters_pix[2, 2] < x_iliac
            or self.field_geometry.isocenters_pix[2, 2] > x_ribs
        ):
            old_iso = self.field_geometry.isocenters_pix[2, 2]
            # Setting the isocenters for arms model at 3/4 space
            self.field_geometry.isocenters_pix[2, 2] = (
                x_iliac + (x_ribs - x_iliac) * 3 / 4
            )
            self.field_geometry.isocenters_pix[3, 2] = (
                x_iliac + (x_ribs - x_iliac) * 3 / 4
            )
            # Adjust the fields (abdomen/pelvis) after the isocenter shift, with an overlap specified in config.yml
            self.field_geometry.jaws_X_pix[2, 1] = (
                self.field_geometry.isocenters_pix[6, 2] - old_iso
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[7, 0]

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

            # Fix thorax isocenter
            self.field_geometry.isocenters_pix[6, 2] = (
                self.field_geometry.isocenters_pix[2, 2]
                + self.field_geometry.isocenters_pix[8, 2]
            ) / 2
            self.field_geometry.isocenters_pix[7, 2] = (
                self.field_geometry.isocenters_pix[2, 2]
                + self.field_geometry.isocenters_pix[8, 2]
            ) / 2

            # Ensure overlap of abdominal field (lower) and pelvic field (upper), with an overlap specified in config.yml
            min_aperture = (
                self.field_geometry.isocenters_pix[0, 2]
                - self.field_geometry.isocenters_pix[2, 2]
                + self.field_geometry.jaws_Y_pix[0, 1]
                - self.field_overlap_pixels
            ) * self.image.aspect_ratio
            if abs(self.field_geometry.jaws_X_pix[3, 0]) < abs(min_aperture):
                self.field_geometry.jaws_X_pix[3, 0] = min_aperture

            # Ensure overlap of abdominal field (upper) and thorax field (lower), with an overlap specified in config.yml
            self.field_geometry.jaws_X_pix[2, 1] = (
                self.field_geometry.isocenters_pix[6, 2]
                - self.field_geometry.isocenters_pix[2, 2]
                + self.field_overlap_pixels
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[7, 0]

            # Ensure overlap of shoulders field (upper) and head field (lower), with an overlap specified in config.yml
            self.field_geometry.jaws_X_pix[6, 1] = (
                self.field_geometry.isocenters_pix[8, 2]
                - self.field_geometry.isocenters_pix[6, 2]
                + self.field_overlap_pixels
            ) * self.image.aspect_ratio + self.field_geometry.jaws_X_pix[9, 0]

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

        self._fit_collimator_head_field()

        logging.info("Adjusted field geometry: %s", self.field_geometry)
