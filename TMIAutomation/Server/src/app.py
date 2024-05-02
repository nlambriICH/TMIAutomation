"""Module implementing the local server of the models."""

import os
import socket
import logging
from flask import Flask, Response, request, jsonify, abort
import onnxruntime
import yaml
import config
from pipeline import Pipeline, RequestInfo

app = Flask(__name__)

try:
    model_path = os.path.join(
        "models", config.MODEL_DIR, f"{config.MODEL_NAME_BODY}.onnx"
    )
    config.ORT_SESSION_BODY = onnxruntime.InferenceSession(model_path)
    logging.info("Loaded model %s from %s.", config.MODEL_NAME_BODY, model_path)
except Exception:  # pylint: disable=broad-exception-caught
    logging.exception(
        "Could not load model %s from %s.", config.MODEL_NAME_BODY, model_path
    )

try:
    model_path = os.path.join(
        "models", config.MODEL_DIR, f"{config.MODEL_NAME_ARMS}.onnx"
    )
    config.ORT_SESSION_ARMS = onnxruntime.InferenceSession(model_path)
    logging.info("Loaded model %s from %s.", config.MODEL_NAME_ARMS, model_path)
except Exception:  # pylint: disable=broad-exception-caught
    logging.exception(
        "Could not load model %s from %s.", config.MODEL_NAME_ARMS, model_path
    )


def _get_available_port() -> int | None:
    """Get the first available port between the range start_port and end_port specified in config.yml.

    Returns:
        int | None: The first available port between start_port and end_port. None if all ports are unavailable.
    """
    start_port = config.YML["start_port"]
    end_port = config.YML["end_port"]
    for port in range(start_port, end_port):
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.connect(("localhost", port))
                logging.info("Port %d already in use. Attempting next port.", port)
                port += 1
        except socket.error:
            return port

    logging.error(
        "Could not find any available port between %d-%d", start_port, end_port
    )

    return None


@app.route("/predict", methods=["POST"])
def predict() -> Response | None:
    """Inference endpoint.

    Returns:
        Response: Response object with application/json mime type containing the
        isocenters, jaw X apertures, and jaw Y apertures in patient coordinate system.
    """
    if request.method == "POST":
        model_name = request.json["model_name"]
        dicom_path = request.json["dicom_path"]
        ptv_name = request.json["ptv_name"]
        oars_name = request.json["oars_name"]

        if model_name == config.MODEL_NAME_BODY and config.ORT_SESSION_BODY is not None:
            ort_session = config.ORT_SESSION_BODY
        elif (
            model_name == config.MODEL_NAME_ARMS and config.ORT_SESSION_ARMS is not None
        ):
            ort_session = config.ORT_SESSION_ARMS
        else:
            abort(503)

        pipeline = Pipeline(
            RequestInfo(model_name, dicom_path, ptv_name, oars_name),
        )
        pipeline_out = pipeline.predict(ort_session)

        return jsonify(
            {
                "Isocenters": pipeline_out[0].tolist(),
                "Jaw_X": pipeline_out[1].tolist(),
                "Jaw_Y": pipeline_out[2].tolist(),
            }
        )

    return None


@app.route("/")
def status_message() -> str:
    """Main endpoint of the local server.

    Returns:
        str: A string reporting a server status message.
    """
    if config.ORT_SESSION_BODY is None and config.ORT_SESSION_ARMS is None:
        return "<p style='color:Red;'>ERROR: Could not load the models.</p>"
    if config.ORT_SESSION_BODY is None:
        return f"<p style='color:Orange;'>WARNING: Could not load {config.MODEL_NAME_BODY} model.</p>"
    if config.ORT_SESSION_ARMS is None:
        return f"<p style='color:Orange;'>WARNING: Could not load {config.MODEL_NAME_ARMS} model.</p>"

    return "<p style='color:Limegreen;'>The local server is running properly!</p>"


def main() -> None:
    """Script entry point."""
    port = _get_available_port()
    if port:
        config.YML["port"] = port
        with open("config.yml", "w", encoding="utf-8") as yml_file:
            yml_file.write(yaml.dump(config.YML, default_flow_style=False))

        logging.info("Starting server on port: %i", port)

        if config.BUNDLED:
            # Running in PyInstaller bundle
            from waitress import serve  # pylint: disable=import-outside-toplevel

            serve(app, host="127.0.0.1", port=port)
        else:
            app.run(port=port)


if __name__ == "__main__":
    main()
