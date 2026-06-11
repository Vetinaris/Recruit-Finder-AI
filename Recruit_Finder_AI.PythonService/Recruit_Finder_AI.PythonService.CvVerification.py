import os
import re
import sys
import json
from datetime import datetime
import time
import pika
import requests
import urllib3

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

try:
    import ollama
except ImportError:
    print("\n" + "="*60)
    print("ERROR: The 'ollama' library is not installed!")
    print("="*60 + "\n")
    sys.exit(1)

API_KEY = os.getenv("API_KEY", "He_ovat_kuin_veden_aarelle_is")
DOTNET_CALLBACK_URL = os.getenv("CV_CALLBACK_URL", "https://web-api:8080/Cv/UpdateAiResult")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "gemma3:4b")
RABBITMQ_HOST = os.getenv("RABBITMQ_HOST", "rabbitmq")

OLLAMA_HOST_URL = os.getenv("OLLAMA_HOST_URL", "http://host.docker.internal:11434")
ollama_client = ollama.Client(host=OLLAMA_HOST_URL)

def verify_ollama_setup():
    print("\n" + "-"*50)
    print("CV SYSTEM STARTUP DIAGNOSTICS:")
    print(f"1. Attempting to connect to Ollama service at {OLLAMA_HOST_URL}...")
    try:
        local_models = ollama_client.list()
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
        response = ollama_client.chat(
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
    print(f"\n--- [MESSAGE CONSUMED] STARTED ANALYSIS FOR CV ID: {cv_id} ---")
    
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
        print(f"--- [CALLBACK FINISHED] .NET NOTIFIED. STATUS CODE: {response.status_code} ---")
    except Exception as e:
        print(f"!!! ERROR OCCURRED WHILE NOTIFYING .NET FOR CV {cv_id}: {e} !!!")

def rabbitmq_callback(ch, method, properties, body):
    print(f"\n [DEBUG] Raw message received: {body.decode()}") 
    try:
        data = json.loads(body)
        cv_id = data.get('cvId')
        print(f" [DEBUG] Processing cvId: {cv_id}")
        if cv_id:
            heavy_ai_processing(cv_id, data)
    except Exception as e:
        print(f" [ERROR] Processing error: {e}")
    finally:
        ch.basic_ack(delivery_tag=method.delivery_tag)

def start_consumer():
    verify_ollama_setup()
    while True:
        try:
            print(f"[*] Attempting to connect to RabbitMQ on host: {RABBITMQ_HOST}")
            connection = pika.BlockingConnection(pika.ConnectionParameters(host=RABBITMQ_HOST))
            channel = connection.channel()
            
            channel.queue_declare(queue='cv_verification_queue', durable=True)
            channel.basic_qos(prefetch_count=1)
            channel.basic_consume(queue='cv_verification_queue', on_message_callback=rabbitmq_callback)
            
            print(' [*] Waiting for your messages. Exit by pressing CTRL+C')
            channel.start_consuming()
        except pika.exceptions.AMQPConnectionError:
            print(" [!] Could not connect to RabbitMQ. Retrying in 5 seconds....")
            time.sleep(5)
        except Exception as e:
            print(f" [!] Wystąpił błąd: {e}. Resetowanie...")
            time.sleep(5)

if __name__ == '__main__':
    start_consumer()