import os
from flask import Flask, request, jsonify
from flask_cors import CORS
import requests
import threading
import time
import urllib3
from dotenv import load_dotenv

load_dotenv()

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

app = Flask(__name__)
CORS(app)

DOTNET_CALLBACK_URL = os.getenv("CV_CALLBACK_URL")
API_KEY = os.getenv("API_KEY")
PORT = int(os.getenv("CV_SERVICE_PORT", 5000))

def heavy_ai_processing(cv_id, title):
    print(f"--- STARTING ANALYSIS FOR ID: {cv_id} ---")
    time.sleep(5)

    payload = {
        "cvId": cv_id,
        "message": "CV analysis completed successfully."
    }

    headers = {
        "X-AI-Key": API_KEY
    }

    try:
        response = requests.post(
            DOTNET_CALLBACK_URL,
            json=payload,
            headers=headers,
            verify=False,
            timeout=10
        )
        print(f"--- .NET NOTIFIED. STATUS: {response.status_code} ---")
    except Exception as e:
        print(f"!!! ERROR NOTIFYING .NET: {e} !!!")


@app.route('/verify-cv', methods=['POST'])
def verify_cv():
    incoming_key = request.headers.get("X-AI-Key")
    if incoming_key != API_KEY:
        return jsonify({"status": "unauthorized"}), 401

    try:
        data = request.json
        cv_id = data.get('cvId')

        thread = threading.Thread(target=heavy_ai_processing, args=(cv_id, "CV Analysis"))
        thread.start()

        return jsonify({"status": "accepted"}), 202
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500


if __name__ == '__main__':
    app.run(port=PORT, debug=True)