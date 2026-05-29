import os
import threading
import requests
import urllib3
import json
import re
from flask import Flask, request, jsonify
from flask_cors import CORS
from dotenv import load_dotenv

load_dotenv()
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

app = Flask(__name__)
CORS(app)

API_KEY = os.getenv("API_KEY")
CALLBACK_URL = os.getenv("OFFER_CALLBACK_URL")
OLLAMA_URL = os.getenv("OLLAMA_URL", "http://localhost:11434/api/generate")
PORT = int(os.getenv("OFFER_SERVICE_PORT", 8000))

def call_ollama_gemma(prompt):
    try:
        response = requests.post(
            OLLAMA_URL,
            json={
                "model": "gemma3:4b",
                "prompt": prompt,
                "stream": False,
                "options": {
                    "temperature": 0.1
                }
            },
            timeout=60
        )
        if response.status_code == 200:
            return response.json().get("response", "")
        return "Błąd: Nie udało się pobrać analizy od AI."
    except Exception as e:
        return f"Błąd połączenia z Ollama: {str(e)}"

def convert_raw_text_to_json(raw_text):

    parsed_data = {
        "description": "Przeprowadzono automatyczną ekstrakcję danych z tekstu strukturalnego AI.",
        "pros": [],
        "cons": [],
        "score": 10
    }
    
    if not raw_text:
        return parsed_data

    score_match = re.search(r'(?:score|match|wynik|punkty)\D*(\d+)', raw_text, re.IGNORECASE)
    if score_match:
        val = int(score_match.group(1))
        parsed_data["score"] = min(max(val, 0), 100)
    else:
        any_num = re.findall(r'\b(\d{1,3})\b', raw_text)
        for num in any_num:
            val = int(num)
            if 0 <= val <= 100:
                parsed_data["score"] = val
                break

    sentences = re.split(r'(?<=[.!?])\s+', raw_text.strip())
    clean_sentences = [s.strip() for s in sentences if s.strip() and not s.strip().startswith(('*', '-', '+', '[', '{', '"'))]
    if clean_sentences:
        parsed_data["description"] = " ".join(clean_sentences[:2])

    current_section = None
    lines = raw_text.split('\n')
    for line in lines:
        line_clean = line.strip().lower()
        if "pro" in line_clean or "zalet" in line_clean or "positive" in line_clean:
            current_section = "pros"
            continue
        elif "con" in line_clean or "wad" in line_clean or "negative" in line_clean or "mismatch" in line_clean:
            current_section = "cons"
            continue
            
        bullet_match = re.match(r'^[\s*\-+\d.]+\s*(.*)', line.strip())
        if bullet_match:
            bullet_text = bullet_match.group(1).strip()
            if bullet_text and len(bullet_text) > 3:
                if current_section == "pros":
                    parsed_data["pros"].append(bullet_text)
                elif current_section == "cons":
                    parsed_data["cons"].append(bullet_text)
        elif line.strip().startswith('"') and len(line.strip()) > 5:
            val = line.strip().strip('"')
            if current_section == "pros":
                parsed_data["pros"].append(val)
            elif current_section == "cons":
                parsed_data["cons"].append(val)

    parsed_data["pros"] = list(set(parsed_data["pros"]))
    parsed_data["cons"] = list(set(parsed_data["cons"]))
    
    if not parsed_data["pros"]:
        parsed_data["pros"] = ["Zweryfikowano profil kandydata."]
    if not parsed_data["cons"] and parsed_data["score"] < 50:
        parsed_data["cons"] = ["Wykryto brak pełnego dopasowania do wymagań oferty."]

    return parsed_data

def clean_and_parse_json(raw_output):

    if not raw_output:
        raise ValueError("Otrzymano pustą odpowiedź od modelu AI.")

    cleaned = raw_output.strip()

    match = re.search(r'(\{.*})', cleaned, re.DOTALL)
    if match:
        cleaned = match.group(1)
    else:
        cleaned = re.sub(r'^```[a-zA-Z]*\s*', '', cleaned)
        cleaned = re.sub(r'\s*```$', '', cleaned)
        cleaned = cleaned.strip()

    try:
        return json.loads(cleaned)
    except Exception as json_err:
        print(f"Błąd składni JSON od AI ({json_err}). Uruchamianie parsera heurystycznego...")
        return convert_raw_text_to_json(raw_output)

def ensure_list(value):

    if isinstance(value, list):
        return [str(item).strip() for item in value if item]
    elif isinstance(value, str):
        cleaned = value.strip()
        return [cleaned] if cleaned else []
    return []

def process_offer_ranking_logic(csharp_payload):
 
    try:
        offer_id = csharp_payload.get("offerId")
        requirements = csharp_payload.get("requirements", "")
        description = csharp_payload.get("description", "")
        title = csharp_payload.get("title", "")
        required_languages = csharp_payload.get("requiredLanguages", "Not specified")
        applications = csharp_payload.get("applications", [])
    except Exception as e:
        print(f"Błąd podczas parsowania payloadu z C#: {e}")
        return False

    analyzed_candidates = []

    for app_data in applications:
        try:
            app_id_raw = app_data.get("applicationId") or app_data.get("id")
            
            try:
                app_id = int(app_id_raw) if app_id_raw is not None else 0
            except (ValueError, TypeError):
                app_id = 0
                print(f"Ostrzeżenie: Nie można rzutować ID '{app_id_raw}' na int. Domyślnie 0.")

            cv = app_data.get("cv", {})
            candidate_name = f"{cv.get('name', 'Ukryte')} {cv.get('surname', 'Nazwisko')}"
            
            experience = cv.get('experience', '')
            education = cv.get('education', '')
            skills = cv.get('skills', '')
            languages = cv.get('languages', '')

            initial_prompt = f"""
            You are an expert AI Generalist Recruiter working across various industries. 
            Evaluate the candidate's alignment with this specific job position using a strict scoring system.
            
            SCORING ALGORITHM (Calculate the final score carefully):
            1. CORE FUNCTIONAL FIT (0-50 points):
               - Does the candidate possess the key skills, tools, and professional experience required for this specific role?
               - Compare the JOB REQUIREMENTS with the CANDIDATE SKILLS and EXPERIENCE.
               - If the candidate is from a completely unrelated field with zero overlapping skills or transferable experience: this section MUST receive 0 points.
               - If they have some transferable or overlapping skills: award 5 to 25 points.
               - If they meet the core skill requirements: award 26 to 50 points.
               
            2. ROLE & EXPERIENCE ALIGNMENT (0-30 points):
               - Is their actual profession, past job titles, and career path aligned with this job role?
               - If their background is from a completely unrelated department/industry: this section MUST receive 0 points.
               - If aligned: award up to 30 points based on their level of seniority and years in relevant roles.
               
            3. LANGUAGE ALIGNMENT (0-20 points):
               - Required languages for this job: {required_languages}
               - Candidate languages: {languages}
               - Check each required language individually. If the candidate clearly speaks a required language (e.g. English C1), do NOT say they lack it. Only apply a penalty if they are genuinely missing that specific language (e.g. Polish).
               - If any required language is missing from the candidate's CV, this section MUST receive 0 points AND you must apply an automatic -20 point penalty to the total score.
            
            4. EXTRA TRANSFERABLE STRENGTHS BONUS (0-10 points boost):
               - Identify valuable professional or technical skills in the candidate's CV that are NOT explicitly requested in the job requirements but represent solid professional capabilities (e.g. script writing, test automation, process optimization, agile project methods, data analysis, or team coordination).
               - Add +2 points for each identified extra strength, up to a maximum of +10 points.
               - This bonus helps differentiate related technical specialists (e.g. QA, DevOps) from completely mismatched profiles (e.g. Accountant), even if neither perfectly fits the primary technology stack.
            
            CRITICAL KNOCK-OUT RULES:
            - If the candidate represents an entirely unrelated, non-technical profession (e.g., an Accountant applying for a Software Developer role) with absolutely no technical or operational overlap, their total score MUST NOT exceed 10 points.
            - Candidates from related technical fields (like DevOps, QA, or SysAdmin) who lack the specific tech stack but possess high transferable technical skills should end up slightly higher (usually between 11 and 25 points) due to the "Extra Transferable Strengths" bonus.
            
            JOB POSITION DETAILS:
            Title: {title}
            Requirements: {requirements}
            Description: {description}
            Required Languages: {required_languages}
            
            CANDIDATE CV ({candidate_name}):
            Experience: {experience}
            Education: {education}
            Skills: {skills}
            Languages: {languages}
            
            Return the result EXCLUSIVELY as a valid JSON object. Do not include markdown code block syntax (like ```json) or introductory explanations.
            
            JSON Schema (Do not use 85 as a default score. Strictly output the calculated score):
            {{
                "description": "A very concise professional summary of the candidate's background, explaining exactly why they fit or why they are completely disqualified.",
                "pros": ["Key strength 1"],
                "cons": ["Key weakness or disqualification reason"],
                "score": 0
            }}
            In the "score" field, provide ONLY an integer from 0 to 100 representing the final calculated match percentage.
            """
            
            initial_output = call_ollama_gemma(initial_prompt)
            
            validation_prompt = f"""
            You are a meticulous Recruitment Quality Assurance Auditor. Verify and correct the generated analysis to ensure it is completely logical, truthful, and free of scoring errors.
            
            CRITICAL AUDITING RULES:
            1. PROFESSION AUDIT: Is the candidate's professional background completely unrelated to the job title "{title}"? 
               - If yes, and they have zero technical overlap (e.g., Accountant applying for a backend role), their final score MUST be corrected to a value between 0 and 10.
               - If they are from a related or technical department (e.g., QA, DevOps, or SysAdmin) but lack specific technologies, they deserve a slightly higher score (usually 11-25) thanks to their extra transferable skills. Ensure this healthy gradation is visible.
            2. DUAL-SKILL AUDIT: Do not penalize candidates who possess additional skills (e.g., project management skills in a specialist role) as long as they meet the primary requirements of the position.
            3. LANGUAGE AUDIT: Double-check the candidate's languages ({languages}) against required ones ({required_languages}). Do NOT make false statements. If the candidate has English C1, do not claim they lack English. If they only lack Polish, explicitly write that they lack Polish, and ensure English is marked as a Pro or left neutral. Reduce the score by 25 points only for the actual missing language.
            
            ORIGINAL JOB DETAILS:
            Title: {title}
            Requirements: {requirements}
            Required Languages: {required_languages}
            
            CANDIDATE DATA:
            Skills: {skills}
            Languages: {languages}
            Experience: {experience}
            
            PROPOSED ANALYSIS TO AUDIT (JSON):
            {initial_output}
            
            Correct any contradictions and apply the critical auditing rules. Return the corrected, finalized JSON matching the exact schema below.
            Return EXCLUSIVELY the final JSON. No markdown code blocks, no chat formatting.
            
            JSON Schema:
            {{
                "description": "Audited and corrected professional summary of the candidate's alignment.",
                "pros": ["Audited Pro"],
                "cons": ["Audited Con"],
                "score": 0
            }}
            """
            
            final_validated_output = call_ollama_gemma(validation_prompt)

            score = 50
            candidate_description = "Błąd walidacji AI."
            pros = []
            cons = []

            try:
                parsed_data = clean_and_parse_json(final_validated_output)
                score = int(parsed_data.get("score", 50))
                candidate_description = parsed_data.get("description", "")
                pros = ensure_list(parsed_data.get("pros", []))
                cons = ensure_list(parsed_data.get("cons", []))
            except Exception as e:
                print(f"Błąd parsowania JSON dla aplikacji {app_id}: {e}. Próba użycia wyniku pierwotnego...")
                
                try:
                    parsed_fallback = clean_and_parse_json(initial_output)
                    score = int(parsed_fallback.get("score", 50))
                    candidate_description = "[Obejście Audytora] " + parsed_fallback.get("description", "")
                    pros = ensure_list(parsed_fallback.get("pros", []))
                    cons = ensure_list(parsed_fallback.get("cons", []))
                except Exception as fe:
                    print(f"Krytyczny błąd parsowania awaryjnego dla aplikacji {app_id}: {fe}")
                    candidate_description = "Krytyczny błąd formatowania odpowiedzi AI."
                    score = 15
                    pros = []
                    cons = ["Nie można przeprowadzić strukturalnego parsowania danych z powodu błędnego formatowania AI."]

            analyzed_candidates.append({
                "applicationId": app_id,
                "score": score,
                "description": candidate_description,
                "pros": pros,
                "cons": cons
            })

        except Exception as candidate_error:
            print(f"Krytyczny błąd przetwarzania kandydata: {candidate_error}")
            try:
                fallback_id = int(app_data.get("applicationId") or app_data.get("id") or 0)
            except:
                fallback_id = 0
                
            analyzed_candidates.append({
                "applicationId": fallback_id,
                "score": 10,
                "description": "Wewnętrzny błąd systemu podczas oceny profilu kandydata.",
                "pros": [],
                "cons": [f"Błąd systemowy: {str(candidate_error)}"]
            })

    analyzed_candidates.sort(key=lambda x: x["score"], reverse=True)

    try:
        report_payload = {
            "offerId": int(offer_id) if offer_id is not None else 0,
            "status": "Verified",
            "results": analyzed_candidates
        }
    except ValueError as e:
        print(f"Błąd rzutowania ID oferty na int: {e}")
        return False

    try:
        response = requests.post(
            CALLBACK_URL, 
            json=report_payload, 
            headers={"X-AI-Key": API_KEY}, 
            verify=False
        )
        print(f"Callback zakończony sukcesem dla Oferty ID {offer_id}. Kod statusu: {response.status_code}")
    except Exception as e:
        print(f"Błąd dostarczania callbacku dla Oferty ID {offer_id}: {e}")

@app.route('/analyze-offer', methods=['POST'])
def analyze_offer():
    if request.headers.get("X-AI-Key") != API_KEY:
        return jsonify({"status": "unauthorized"}), 401
    
    data = request.json
    if not data or not data.get('offerId'):
        return jsonify({"status": "bad_request", "message": "Missing offerId"}), 400

    threading.Thread(
        target=process_offer_ranking_logic, 
        args=(data,) 
    ).start()
    
    return jsonify({
        "status": "accepted", 
        "offerId": data.get('offerId'),
        "count_received": len(data.get('applications', []))
    }), 202

if __name__ == '__main__':
    app.run(port=PORT, debug=True)