"""Configuration module."""
import os
import sys
import logging
import yaml
from onnxruntime import InferenceSession

# True if running in PyInstaller bundle
BUNDLED: bool = getattr(sys, "frozen", False) and hasattr(sys, "_MEIPASS")

MODEL_NAME_BODY: str = "body_cnn"
MODEL_NAME_ARMS: str = "arms_cnn"

ORT_SESSION_BODY: InferenceSession | None = None
ORT_SESSION_ARMS: InferenceSession | None = None

if not os.path.exists("logs"):
    os.makedirs("logs")

logging.basicConfig(
    filename="logs/app.log",
    level=logging.INFO,
    format="%(asctime)s:%(name)s:%(levelname)s:%(message)s",
)

with open("config.yml", "r", encoding="utf-8") as stream:
    try:
        YML = yaml.safe_load(stream)
        logging.getLogger().setLevel(YML["log_level"])
    except yaml.YAMLError as exc:
        logging.error(exc)
