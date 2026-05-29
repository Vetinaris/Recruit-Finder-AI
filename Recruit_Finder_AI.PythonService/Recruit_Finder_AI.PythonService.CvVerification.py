import os
import re
import sys
import json
from datetime import datetime
import threading
from flask import Flask, request, jsonify
from flask_cors import CORS
import requests
import urllib3
from dotenv import load_dotenv

load_dotenv()
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

try:
    import ollama
except ImportError:
    print("\n" + "="*60)
    print("ERROR: The 'ollama' library is not installed!")
    print("Run in terminal: pip install ollama")
    print("="*60 + "\n")
    sys.exit(1)

app = Flask(__name__)
CORS(app)

DOTNET_CALLBACK_URL = os.getenv("CV_CALLBACK_URL")
API_KEY = os.getenv("API_KEY")
PORT = int(os.getenv("CV_SERVICE_PORT", 5000))

OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "gemma3:4b")

def verify_ollama_setup():
    print("\n" + "-"*50)
    print("CV SYSTEM STARTUP DIAGNOSTICS:")
    print(f"1. Attempting to connect to Ollama service...")
    try:
        local_models = ollama.list()
        print("   [OK] Connection to Ollama established successfully.")
        
        print(f"2. Checking availability of model '{OLLAMA_MODEL}'...")
        models_list = [m.get('model', m.get('name', '')) for m in local_models.get('models', [])]
        model_exists = any(OLLAMA_MODEL in m for m in models_list)
        
        if model_exists:
            print(f"   [OK] Model '{OLLAMA_MODEL}' is installed and ready.")
        else:
            print(f"   [WARNING] Model '{OLLAMA_MODEL}' was not found locally!")
            print(f"   Make sure to download it using: ollama pull {OLLAMA_MODEL}")
            print("   Available models:", ", ".join(models_list) if models_list else "No pulled models found.")
            
    except Exception as e:
        print("   [CRITICAL ERROR] Cannot connect to Ollama application!")
        print("   Ensure that Ollama is running on your machine.")
        print(f"   Details: {e}")
    print("-"*50 + "\n")

def parse_years_from_text(text):
    if not text:
        return None, None
    years = [int(y) for y in re.findall(r'\b(19\d{2}|20\d{2})\b', str(text))]
    if not years:
        return None, None
    return min(years), max(years)

def check_timeline_heuristics(data):

    birth_date_str = data.get('dateOfBirth', '')
    if not birth_date_str:
        return False, "Missing date of birth."
        
    try:
        birth_year = datetime.strptime(birth_date_str.strip(), "%Y-%m-%d").year
    except ValueError:
        try:
            birth_year = datetime.strptime(birth_date_str.strip(), "%d.%m.%Y").year
        except ValueError:
            return False, f"Invalid date of birth format '{birth_date_str}'. Expected YYYY-MM-DD."

    current_year = datetime.now().year
    age_today = current_year - birth_year

    if age_today < 16:
        return False, f"Candidate's age ({age_today} years old) is below the legal working age limit."

    edu_min, edu_max = parse_years_from_text(data.get('education', ''))
    if edu_min and edu_min < birth_year + 15:
        return False, f"Education timeline conflict: University start year ({edu_min}) is unrealistic relative to the birth year ({birth_year})."

    exp_min, exp_max = parse_years_from_text(data.get('experience', ''))
    if exp_min and exp_min < birth_year + 14:
        return False, f"Employment timeline conflict: Job start year ({exp_min}) is physically impossible relative to the birth year ({birth_year})."

    return True, ""

def basic_heuristic_check(data):
    full_text = f"{data.get('fullName', '')} {data.get('experience', '')} {data.get('education', '')}"
    
    if re.search(r'(.)\1{9,}', full_text):
        return False, "Suspicious character repetitions detected (potential spam)."
        
    emoji_pattern = re.compile(r'[\U00010000-\U0010ffff]', flags=re.UNICODE)
    if len(emoji_pattern.findall(full_text)) > 5:
        return False, "The CV contains an excessive amount of emojis."
        
    return True, ""

def convert_raw_text_to_json(raw_text, timeline_ok, python_error_msg, candidate_name):
    parsed_data = {
        "isValid": True if timeline_ok else False,
        "feedback": f"Hello {candidate_name}, your CV verification has completed."
    }
    
    if not raw_text:
        if not timeline_ok:
            parsed_data["feedback"] = f"Hello {candidate_name}. Verification failed: {python_error_msg}"
        return parsed_data

    raw_text_lower = raw_text.lower()
    
    if not timeline_ok:
        parsed_data["isValid"] = False
    else:
        negative_signals = ["reject", "invalid", "unrealistic", "timeline conflict", "incorrect", "adjustments", "failed"]
        positive_signals = ["accept", "valid", "passed", "successfully", "consistent", "approved"]
        
        neg_count = sum(1 for signal in negative_signals if signal in raw_text_lower)
        pos_count = sum(1 for signal in positive_signals if signal in raw_text_lower)
        
        if neg_count > pos_count:
            parsed_data["isValid"] = False
        else:
            parsed_data["isValid"] = True

    sentences = re.split(r'(?<=[.!?])\s+', raw_text.strip())
    clean_sentences = []
    for s in sentences:
        s_strip = s.strip()
        if s_strip and not s_strip.startswith(('*', '-', '+', '[', '{', '"', '}', 'isValid', 'feedback')):
            clean_sentences.append(s_strip)
            
    if clean_sentences:
        parsed_data["feedback"] = " ".join(clean_sentences[:3])
    else:
        fallback_msg = re.sub(r'[\{\}\"\'\[\]]|\bisValid\b|\bfeedback\b', '', raw_text).strip()
        fallback_msg = re.sub(r'^[:\s]+', '', fallback_msg)
        if len(fallback_msg) > 10:
            parsed_data["feedback"] = fallback_msg[:300]
        else:
            if parsed_data["isValid"]:
                parsed_data["feedback"] = f"Dear {candidate_name}, your CV has been successfully verified and meets all guidelines."
            else:
                parsed_data["feedback"] = f"Dear {candidate_name}, verification failed: {python_error_msg if python_error_msg else 'Timeline and consistency conflict.'}"

    return parsed_data

def clean_and_parse_json(raw_output, timeline_ok, python_error_msg, candidate_name):
  
    if not raw_output:
        return convert_raw_text_to_json("", timeline_ok, python_error_msg, candidate_name)

    cleaned = raw_output.strip()

    match = re.search(r'(\{.*\})', cleaned, re.DOTALL)
    if match:
        cleaned = match.group(1)
    else:
        cleaned = re.sub(r'^```[a-zA-Z]*\s*', '', cleaned)
        cleaned = re.sub(r'\s*```$', '', cleaned)
        cleaned = cleaned.strip()

    try:
        parsed = json.loads(cleaned, strict=False)
        return {
            "isValid": bool(parsed.get("isValid", timeline_ok)),
            "feedback": str(parsed.get("feedback", f"Hello {candidate_name}, verification completed."))
        }
    except Exception as json_err:
        print(f"   [AI PARSING WARNING] Failed to parse JSON: {json_err}. Launching Emergency Fallback Parser...")
        return convert_raw_text_to_json(raw_output, timeline_ok, python_error_msg, candidate_name)

def analyze_cv_with_ollama(data, timeline_ok, python_error_msg):
    birth_date_str = data.get('dateOfBirth', 'Unknown')
    current_year = datetime.now().year
    candidate_name = data.get('fullName', 'Candidate')
    
    timeline_status = "PASSED" if timeline_ok else f"FAILED - Reason: {python_error_msg}"
    
    prompt = f"""You are an expert HR Content Auditor. Verify if the CV content is professional, logically sound, and realistic.

Candidate's Name: {candidate_name}
Current Year: {current_year}

CRITICAL TIMELINE STATUS (Calculated by system, DO NOT OVERRIDE):
Timeline Status: {timeline_status}

CV CONTENT TO EVALUATE:
Professional Experience: {data.get('experience', 'None')}
Education: {data.get('education', 'None')}
Skills: {data.get('skills', 'None')}

EVALUATION CRITERIA:
1. Timeline Rule: If 'Timeline Status' is FAILED, you MUST reject the CV and explain the exact reason in feedback.
2. Realism Check: Reject jokes, obvious sci-fi elements (e.g. "Astronaut on Mars"), gibberish, or plain nonsense.
3. Language of Feedback: The "feedback" string MUST be a direct, polite, personalized message addressed to {candidate_name} in English.

Return the result EXCLUSIVELY as a valid JSON object matching this schema:
{{
    "isValid": true or false,
    "feedback": "Direct message to {candidate_name} explaining the decision."
}}"""
    
    try:
        response = ollama.chat(
            model=OLLAMA_MODEL,
            messages=[{'role': 'user', 'content': prompt}],
            options={'temperature': 0.1}
        )
        
        raw_output = response['message']['content'].strip()
        
        result_dict = clean_and_parse_json(raw_output, timeline_ok, python_error_msg, candidate_name)
        
        if not timeline_ok:
            result_dict["isValid"] = False
            
        return result_dict.get("isValid", False), result_dict.get("feedback", "Verification completed.")
            
    except Exception as e:
        print(f"   [AI ERROR] Critical error during communication with Ollama: {e}")
        if timeline_ok:
            return True, f"Hello {candidate_name}, your CV timeline validation passed successfully."
        else:
            return False, f"Hello {candidate_name}, validation failed: {python_error_msg}"

def heavy_ai_processing(cv_id, data):
    print(f"\n--- [BACKGROUND THREAD] STARTED ANALYSIS FOR CV ID: {cv_id} ---")
    
    is_heuristic_ok, heuristic_msg = basic_heuristic_check(data)
    
    if not is_heuristic_ok:
        print(f" -> [HEURISTIC REJECTED CV]")
        is_valid = False
        feedback = f"Hello {data.get('fullName', 'Candidate')}. {heuristic_msg}"
    else:
        timeline_ok, python_error_msg = check_timeline_heuristics(data)
        print(f" -> [PYTHON TIMELINE CHECK] Status: {timeline_ok} | Errors: {python_error_msg or 'None'}")
        
        print(f" -> Sending verification request to Ollama ({OLLAMA_MODEL})...")
        is_valid, feedback = analyze_cv_with_ollama(data, timeline_ok, python_error_msg)
        print(f" -> [OLLAMA RESULT] isValid: {is_valid} | feedback: {feedback}")

    payload = {
        "Id": cv_id,
        "IsVerified": is_valid,
        "AiFeedback": feedback
    }

    headers = {
        "X-AI-Key": API_KEY
    }
    if not DOTNET_CALLBACK_URL or DOTNET_CALLBACK_URL.strip().lower() in ["none", ""]:
        print("\n--- [ERROR: CALLBACK URL NOT DEFINED] ---")
        print("CV_CALLBACK_URL is missing or evaluated as 'None' in your .env variables!")
        print("Verification process completed successfully but unable to notify .NET API.")
        print("Please configure CV_CALLBACK_URL in your .env file (e.g., CV_CALLBACK_URL=https://localhost:7187/UpdateAiResult).")
        print("-----------------------------------------\n")
        return

    try:
        print(f" -> Sending callback report to .NET at: {DOTNET_CALLBACK_URL}")
        response = requests.post(
            DOTNET_CALLBACK_URL,
            json=payload,
            headers=headers,
            verify=False,
            timeout=15
        )
        
        print(f"\n--- [DEBUG ODPOWIEDZI Z .NET] ---")
        print(f"Status HTTP: {response.status_code}")
        print(f"Treść (Body): {response.text}")
        print(f"---------------------------------\n")
        print(f"--- [BACKGROUND THREAD FINISHED] .NET NOTIFIED. STATUS: {response.status_code} ---")
    except Exception as e:
        print(f"!!! ERROR OCCURRED WHILE NOTIFYING .NET FOR CV {cv_id}: {e} !!!")


@app.route('/verify-cv', methods=['POST'])
def verify_cv():
    incoming_key = request.headers.get("X-AI-Key")
    if incoming_key != API_KEY:
        return jsonify({"status": "unauthorized"}), 401

    try:
        data = request.json
        cv_id = data.get('cvId')
        thread = threading.Thread(target=heavy_ai_processing, args=(cv_id, data))
        thread.start()
        return jsonify({"status": "accepted", "cvId": cv_id}), 202
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500


if __name__ == '__main__':
    verify_ollama_setup()
    
    print(f"Starting server on port {PORT}...")
    app.run(port=PORT, debug=False)