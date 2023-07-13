from typing import Literal
import numpy as np
from pydicom import Dataset
from rt_utils.image_helper import (
    get_patient_to_pixel_transformation_matrix,
    get_pixel_to_patient_transformation_matrix,
    apply_transformation_to_3d_points,
)


def get_zero_row_idx(arr: np.ndarray) -> np.ndarray:
    """
    Return the indices of rows in a 2D numpy array where all elements are zero.

    Parameters:
    arr (np.ndarray): A 2D numpy array.

    Returns:
    A 1D numpy array of indices of rows where all elements are zero.
    """
    return np.where(np.all(arr == 0, axis=1))[0]


def get_jaw_kps_from_aperture(
    isocenters: np.ndarray,
    jaw_X: np.ndarray,
    jaw_Y: np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    """Compute the 4 middle points of the sides of the rectangle defined by each field's X and Y apertures.

    Args:
        isocenters (np.ndarray): Array of shape (n_fields, 3) containing the 3D coordinates of the isocenters for each field.
        jaw_X (np.ndarray): A 2D array of shape (n_fields, 2) containing the X aperture of the fields, where
                            the 2nd dimension is for X1 and X2.
        jaw_Y (np.ndarray): A 2D array of shape (n_fields, 2) containing the Y aperture of the fields, where
                            the 2nd dimension is for Y1 and Y2.

    Returns:
        A tuple containing two 2D arrays of shape (n_fields, 2):
        - The first array contains the coordinates of the X1 and X2 sides of the field's X aperture.
        - The second array contains the coordinates of the Y1 and Y2 sides of the field's Y aperture.
    """
    # Compute jaw_X/jaw_Y keypoints position: 4 middle points rectangle sides
    # Reshape to be able to broadcast operation
    # Resulting shape=(n_fields, 2)
    jaw_X_kps = isocenters[:, 0].reshape(-1, 1) - jaw_X
    jaw_Y_kps = isocenters[:, 2].reshape(-1, 1) + jaw_Y

    return jaw_X_kps, jaw_Y_kps


def get_jaw_aperture_from_kps(
    isocenters: np.ndarray,
    jaw_X_kps: np.ndarray,
    jaw_Y_kps: np.ndarray,
) -> tuple[np.ndarray, np.ndarray]:
    """Compute the X and Y apertures of each field from the 4 middle points of the sides of the rectangle
    defined by each field's X and Y aperture.

    Args:
        isocenters (np.ndarray): Array of shape (n_fields, 3) containing the 3D coordinates of the isocenters.
        jaw_X_kps (np.ndarray): A 2D array of shape (n_fields, 2) containing the X coordinates of the 4 middle points
                                of the sides of the rectangle defined by each field's X aperture.
        jaw_Y_kps (np.ndarray): A 2D array of shape (n_fields, 2) containing the Y coordinates of the 4 middle points
                                of the sides of the rectangle defined by each field's Y aperture.

    Returns:
        A tuple containing two 2D arrays of shape (n_fields, 2):
        - The first array contains the X aperture of each field, where the 2nd dimension is for X1 and X2.
        - The second array contains the Y aperture of each field, where the 2nd dimension is for Y1 and Y2.
    """
    # Compute jaw_X/jaw_Y apertures
    # Reshape to be able to broadcast operation
    # Resulting shape=(n_fields, 2)
    jaw_X = isocenters[:, 0].reshape(-1, 1) - jaw_X_kps
    jaw_Y = jaw_Y_kps - isocenters[:, 2].reshape(-1, 1)

    return jaw_X, jaw_Y


def transform_field_geometry(
    series_data: list[Dataset],
    iso_orig: np.ndarray,
    jaw_X_orig: np.ndarray,
    jaw_Y_orig: np.ndarray,
    from_to: Literal["pat_pix", "pix_pat"] = "pat_pix",
) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    """
    Transform the field geometry from patient's coordinate system to pixel space or vice versa.

    Args:
        series_data (list[Dataset]): list of DICOM datasets corresponding to the CT series that the RTPLAN belongs to.
        iso_orig (np.ndarray): Array of shape (n_fields, 3) containing the 3D coordinates
            of the isocenter for each field in the original coordinate system.
        jaw_X_orig (np.ndarray): Array of shape (n_fields, 2) containing the X apertures
            for each field in the original coordinate system.
        jaw_Y_orig (np.ndarray): Array of shape (n_fields, 2) containing the Y apertures
            for each field in the original coordinate system.
        from_to (str): the orignal and target coordinate system. Allowed values: "pat_pix" and "pix_pat". Default is "pat_pix".

    Returns:
        tuple[np.ndarray, np.ndarray, np.ndarray]: A tuple containing the transformed isocenters,
            jaw X apertures, and jaw Y apertures. Each of these arrays has the same shape as the
            corresponding input arrays.
    """
    if from_to == "pat_pix":
        transf_matrix = get_patient_to_pixel_transformation_matrix(series_data)
    elif from_to == "pix_pat":
        transf_matrix = get_pixel_to_patient_transformation_matrix(series_data)
    else:
        raise ValueError(f'from_to must be "pat_pix" or "pix_pat" but was {from_to}')

    iso_transf = apply_transformation_to_3d_points(iso_orig, transf_matrix)
    # Assign zero where all isocenter's coord=0
    iso_transf[get_zero_row_idx(iso_orig)] = 0

    jaw_X_kps, jaw_Y_kps = get_jaw_kps_from_aperture(iso_orig, jaw_X_orig, jaw_Y_orig)

    # Reshape to (24, 1)
    # Append (y, z) isocenters coordinates to obtain 3d vectors for transformation matrix
    jaw_X_kps_vect = np.append(
        jaw_X_kps.reshape(-1, 1),
        np.repeat(iso_orig[:, 1:], 2, axis=0),  # repeat coords for each aperture
        axis=1,
    )
    # Reshape to (12, 2) to recover the original shape
    # Assign zero where all original X jaw apertures=0
    jaw_X_kps_transf = apply_transformation_to_3d_points(jaw_X_kps_vect, transf_matrix)[
        :, 0  # Keep only x-coords
    ]
    jaw_X_kps_transf = jaw_X_kps_transf.reshape(jaw_X_kps.shape)
    jaw_X_kps_transf[get_zero_row_idx(jaw_X_orig)] = 0

    # Reshape to (24, 1)
    # Prepend (x, y) isocenters coordinates to obtain 3d vectors for transformation matrix
    # Note: jaw_Y is along the z-axis
    jaw_Y_kps_vect = np.insert(
        jaw_Y_kps.reshape(-1, 1),
        [0],
        np.repeat(iso_orig[:, :2], 2, axis=0),  # repeat coords for each aperture
        axis=1,
    )
    # Reshape to (12, 2) to recover the original shape
    # Assign zero where all original Y jaw apertures=0
    jaw_Y_kps_transf = apply_transformation_to_3d_points(jaw_Y_kps_vect, transf_matrix)[
        :, 2  # Keep only z-coords
    ]
    jaw_Y_kps_transf = jaw_Y_kps_transf.reshape(jaw_Y_kps.shape)
    jaw_Y_kps_transf[get_zero_row_idx(jaw_Y_orig)] = 0

    jaw_X_transf, jaw_Y_transf = get_jaw_aperture_from_kps(
        iso_transf, jaw_X_kps_transf, jaw_Y_kps_transf
    )

    return iso_transf, jaw_X_transf, jaw_Y_transf
