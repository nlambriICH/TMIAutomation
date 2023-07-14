"""Module implementing the local server of the models."""
import socket
import logging
from flask import Flask, Response, request, jsonify, abort
import onnxruntime
import yaml
from pipeline import Pipeline, RequestInfo


logging.basicConfig(
    filename="logs/app.log",
    level=logging.INFO,
    format="%(asctime)s:%(name)s:%(levelname)s:%(message)s",
)

with open("config.yml", "r", encoding="utf-8") as stream:
    try:
        config = yaml.safe_load(stream)
        logging.getLogger().setLevel(config["loglevel"])
    except yaml.YAMLError as exc:
        logging.error(exc)

app = Flask(__name__)


try:
    ort_session = onnxruntime.InferenceSession(r"models\body_cnn.onnx")
    input_name = ort_session.get_inputs()[0].name
except Exception:  # pylint: disable=broad-exception-caught
    logging.exception("Could not load model.")


def _get_available_port() -> int | None:
    """Get the first available port between the range startport and endport specified in config.yml.

    Returns:
        int | None: The first available port between startport and endport. None if all ports are unavailable.
    """
    start_port = config["startport"]
    end_port = config["endport"]
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
    if ort_session is None:
        abort(503)

    if request.method == "POST":
        try:
            dicom_path = request.json["dicom_path"]
        except KeyError:
            logging.warning(
                "Dicom path not found in request. Using value from config.yml."
            )
            dicom_path = config["dicom_path"]
        ptv_name = request.json["ptv_name"]
        oars_name = request.json["oars_name"]

        pipeline = Pipeline(
            ort_session,
            input_name,
            RequestInfo(dicom_path, ptv_name, oars_name),
            save_io=True,
        )
        pipeline_out = pipeline.predict()

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
    return (
        "<p>ERROR: Could not load the model.</p>"
        if ort_session is None
        else "<p>The local server is running properly!</p>"
    )


def main() -> None:
    """Script entry point."""
    port = _get_available_port()
    if port:
        config["port"] = port
        with open("config.yml", "w", encoding="utf-8") as yml_file:
            yml_file.write(yaml.dump(config, default_flow_style=False))
        logging.info("Starting server on port: %i", port)
        app.run(port=port, debug=True)


if __name__ == "__main__":
    main()
