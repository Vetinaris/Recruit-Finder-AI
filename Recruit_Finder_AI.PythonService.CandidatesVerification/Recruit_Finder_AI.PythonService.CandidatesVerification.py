import os, threading, time, requests, urllib3
from flask import Flask, request, jsonify
from flask_cors import CORS
from dotenv import load_dotenv

load_dotenv()
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

app = Flask(__name__)
CORS(app)

API_KEY = os.getenv("API_KEY")
CALLBACK_URL = os.getenv("OFFER_CALLBACK_URL")
PORT = int(os.getenv("OFFER_SERVICE_PORT", 8000))
def process_offer_logic(offer_id, index):
    time.sleep(5)
    
    candidates = [
        f"Candidate {i+1}: Profile match at {(95 - i*2)}%." 
        for i in range(10)
    ]
    
    analysis_report = "Top 10 recommended candidates for this position:\n\n" + "\n".join(candidates)
    
    payload = {
        "offerId": offer_id,
        "status": "Verified",
        "message": analysis_report
    } 
    

    try:
        response = requests.post(
            CALLBACK_URL, 
            json=payload, 
            headers={"X-AI-Key": API_KEY}, 
            verify=False
        )
        print(f"Callback wysłany dla ID {offer_id}. Status C#: {response.status_code}")
    except Exception as e:
        print(f"Błąd podczas wysyłania callbacku: {e}")

@app.route('/analyze-offer', methods=['POST'])
def analyze_offer():
    if request.headers.get("X-AI-Key") != API_KEY:
        return jsonify({"status": "unauthorized"}), 401
    
    data = request.json
    offers = data.get('offerIds', []) 
    
    if not offers and data.get('offerId'):
        offers = [data.get('offerId')]
    to_process = offers[:10]

    for index, offer_id in enumerate(to_process):
        threading.Thread(target=process_offer_logic, args=(offer_id, index)).start()
    
    return jsonify({
        "status": "accepted", 
        "count_received": len(offers), 
        "count_processing": len(to_process)
    }), 202

if __name__ == '__main__':
    app.run(port=PORT, debug=True)