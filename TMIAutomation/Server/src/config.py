"""Configuration module."""
import sys
from onnxruntime import InferenceSession

# True if running in PyInstaller bundle
BUNDLED: bool = getattr(sys, "frozen", False) and hasattr(sys, "_MEIPASS")

MODEL_NAME_BODY: str = "body_cnn"
MODEL_NAME_ARMS: str = "arms_cnn"

ORT_SESSION_BODY: InferenceSession | None = None
ORT_SESSION_ARMS: InferenceSession | None = None
