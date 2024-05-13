"""Module implementing debug visualizations."""

import os
import numpy as np
import matplotlib
import matplotlib.pyplot as plt
import matplotlib.patches as mpatches
from src.pipeline import Image, FieldGeometry
from src.local_optimization.local_optimization import LocalOptimization
from src import config

matplotlib.use("agg")


def save_input_img(patient_id: str, image: Image) -> None:
    """Save the input image for the model.

    Args:
        patient_id (str): The patient ID used to name the saved image.
        image (Image): The original image with shape (H, W, C).
    """
    if not os.path.exists("logs/input_img/"):
        os.makedirs("logs/input_img/")

    plt.imsave(
        f"logs/input_img/input_img_{patient_id}.png",
        image.pixels,
    )


def save_local_opt(
    patient_id: str, image: Image, local_optimization: LocalOptimization
) -> None:
    """Save the image and the local optimization results for the optimal 'x' pixel locations of iliac crests and ribs.
    Four horizontal lines define the two 'y' regions where the search is limited. Two vertical red lines show the 'x' pixel
    location found by the local optimization for the iliac crests and ribs.

    Args:
        patient_id (str): The patient ID used to name the saved image.
        image (Image): The original image with shape (H, W, C).
        local_optimization (LocalOptimization): The local optimization instance containing the
        optimization search space and optimization results.
    """
    plt.imshow(image.pixels[..., 1], cmap="gray", aspect=1 / image.aspect_ratio)
    plt.vlines(
        [
            local_optimization.optimization_search_space.x_pixel_left,
            local_optimization.optimization_search_space.x_pixel_right,
        ],
        local_optimization.optimization_search_space.y_pixels_right[0],
        local_optimization.optimization_search_space.y_pixels_left[-1],
        linewidths=0.5,
    )
    plt.vlines(
        [
            local_optimization.optimization_result.x_pixel_ribs,
            local_optimization.optimization_result.x_pixel_iliac,
        ],
        local_optimization.optimization_search_space.y_pixels_right[0],
        local_optimization.optimization_search_space.y_pixels_left[-1],
        linewidths=0.5,
        colors="r",
        linestyles="--",
    )
    for y in (
        local_optimization.optimization_search_space.y_pixels_right,
        local_optimization.optimization_search_space.y_pixels_left,
    ):
        plt.hlines(
            y[[0, -1]],
            local_optimization.optimization_search_space.x_pixel_left,
            local_optimization.optimization_search_space.x_pixel_right,
            linewidths=0.5,
        )

    if not os.path.exists("logs/local_opt/"):
        os.makedirs("logs/local_opt/")

    plt.savefig(f"logs/local_opt/local_opt_{patient_id}.png")
    plt.close()


def save_field_geometry(
    patient_id: str, model_name: str, image: Image, field_geometry: FieldGeometry
) -> None:
    """Save the image and field geometry.

    Args:
        patient_id (str): The patient ID used to name the saved image.
        model_name (str): The model name.
        image (Image): The original image with shape (H, W, C).
        field_geometry (FieldGeometry): The field geometry in pixel space.
    """
    plt.imshow(
        image.pixels[..., 0],
        cmap="gray",
        aspect=1 / image.aspect_ratio,
    )

    num_iso = field_geometry.isocenters_pix.shape[0]

    # Same color for each isocenter group and arms
    # Different linestyle for fields of the same group
    palette_plots = [
        color for color in plt.color_sequences["tab10"][:num_iso] for _ in range(2)
    ]
    linestyles = ["--", "-"] * (num_iso // 2)

    angles = np.repeat(90, num_iso)
    if model_name == config.MODEL_NAME_ARMS:
        linestyles[-2] = "-"  # same linestyle for isocenters on the arms
        angles[-2:] = 0
    if config.YML["coll_pelvis"]:
        angles[:2] = 0

    for i, (iso, jaw_X, jaw_Y, angle) in enumerate(
        zip(
            field_geometry.isocenters_pix,
            field_geometry.jaws_X_pix,
            field_geometry.jaws_Y_pix,
            angles,
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

        plt.scatter(
            iso[2],
            iso[0],
            color=palette_plots[i],
            s=30,
        )

        plt.gca().add_patch(
            mpatches.Rectangle(
                (iso_pixel_col + offset_col, iso_pixel_row - offset_row),
                width,
                height,
                angle=angle,
                rotation_point=(iso_pixel_col, iso_pixel_row),
                linestyle=linestyles[i],
                linewidth=2,
                edgecolor=palette_plots[i],
                facecolor="none",
            )
        )

    if not os.path.exists("logs/field_geometry/"):
        os.makedirs("logs/field_geometry/")

    plt.savefig(f"logs/field_geometry/field_geometry_{patient_id}.png")
    plt.close()
