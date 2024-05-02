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

MODEL_OUTPUT_ARMS_90: int = 30
MODEL_OUTPUT_BODY_90: int = 25
MODEL_OUTPUT_BODY_5_355: int = 19
MODEL_OUTPUT_ARMS_5_355: int = 24

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

if YML["coll_pelvis"]:
    MODEL_DIR: str = "5_355"
else:
    MODEL_DIR: str = "90"
