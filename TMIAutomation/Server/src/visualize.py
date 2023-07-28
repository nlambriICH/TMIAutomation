import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from pipeline import Image, FieldGeometry
import config


def save_local_opt(
    image: Image,
    best_x_pixel_spine: float,
    y_pixels_right: np.ndarray,
    y_pixels_left: np.ndarray,
    x_pixel_ribs: int,
    x_pixel_iliac: int,
    x_search_range: int = 40,
) -> None:
    """Save the image used to search for the optimal 'x' pixel locations of iliac crests and ribs.
    A vertical line is drawn to show the starting points of the 'x' search. Four horizontal lines
    define the two 'y' regions where the search is limited. Two vertical red lines show the 'x' pixel
    location found by the local optimization for the iliac crests and ribs.

    Args:
        image (Image): The original image with shape (H, W, C).
        best_x_pixel_spine (float): Optimal 'x' pixel location of the spine.
        y_pixels_right (np.ndarray): 'y' pixel values limiting the search space on the right of the patient.
        y_pixels_left (np.ndarray): 'y' pixel values limiting the search space on the left of the patient.
        x_pixel_ribs (int): 'x' pixel location of the ribs found by the local optimization.
        x_pixel_iliac (int): 'x' pixel value of the iliac crests found by the local optimization.
        x_search_range (int, optional):'x' pixels range of the search space. Defaults to 40.
    """
    plt.imshow(image.pixels[..., 1], cmap="gray", aspect=1 / image.aspect_ratio)
    plt.vlines(best_x_pixel_spine, y_pixels_right[0], y_pixels_left[-1], linewidths=0.5)
    plt.vlines(
        [x_pixel_ribs, x_pixel_iliac],
        y_pixels_right[0],
        y_pixels_left[-1],
        linewidths=0.5,
        colors="r",
        linestyles="--",
    )
    for y in (y_pixels_right, y_pixels_left):
        plt.hlines(
            y[[0, -1]],
            best_x_pixel_spine - x_search_range,
            best_x_pixel_spine + x_search_range,
            linewidths=0.5,
        )
    plt.savefig("logs/local_opt.png")
    plt.close()


def save_field_geometry(
    model_name: str, image: Image, field_geometry: FieldGeometry
) -> None:
    """Save the image and field geometry.

    Args:
        model_name (str): The model name.
        image (Image): The original image with shape (H, W, C).
        field_geometry (FieldGeometry): The field geometry in pixel space.
    """
    plt.imshow(
        image.pixels[..., 0],
        cmap="gray",
        aspect=1 / image.aspect_ratio,
    )

    plt.scatter(
        field_geometry.isocenters_pix[:, 2],
        field_geometry.isocenters_pix[:, 0],
        color="red",
        s=7,
    )

    angles = np.repeat(90, field_geometry.isocenters_pix.shape[0])
    if model_name == config.MODEL_NAME_ARMS:
        angles[-2:] = 0

    for i, (iso, jaw_X, jaw_Y, angle, color) in enumerate(
        zip(
            field_geometry.isocenters_pix,
            field_geometry.jaws_X_pix,
            field_geometry.jaws_Y_pix,
            angles,
            ["b", "r"] * (field_geometry.isocenters_pix.shape[0] // 2),
        )
    ):
        if all(iso == 0):
            continue  # isocenter not present, skip field
        if model_name == config.MODEL_NAME_ARMS and i in [
            4,
            5,
        ]:  # skip thorax isocenter
            continue

        iso_pixel_col, iso_pixel_row = iso[2], iso[0]
        offset_col = jaw_Y[0]
        offset_row = jaw_X[1]
        width = jaw_Y[1] - jaw_Y[0]
        height = jaw_X[1] - jaw_X[0]

        if angle == 90:
            # Change rectangle geometry due to image aspect ratio and collimator angle = 90 degrees
            offset_col *= image.aspect_ratio
            offset_row /= image.aspect_ratio
            width *= image.aspect_ratio
            height /= image.aspect_ratio

        plt.gca().add_patch(
            mpatches.Rectangle(
                (iso_pixel_col + offset_col, iso_pixel_row - offset_row),
                width,
                height,
                angle=angle,
                rotation_point=(iso_pixel_col, iso_pixel_row),
                linestyle="-" if color == "r" else "--",
                linewidth=0.5,
                edgecolor=color,
                facecolor="none",
            )
        )

    plt.savefig("logs/field_geometry.png")
    plt.close()
