import os
import smtplib
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from flask import Flask, request, jsonify
from dotenv import load_dotenv

load_dotenv()

app = Flask(__name__)

SMTP_SERVER = os.getenv('MAIL_SERVER', 'smtp.gmail.com')
SMTP_PORT = int(os.getenv('MAIL_PORT', 587))
MAIL_USERNAME = os.getenv('MAIL_USERNAME')
MAIL_PASSWORD = os.getenv('MAIL_PASSWORD')
SERVICE_KEY = os.getenv('EMAIL_SERVICE_KEY')

def send_email_template(recipient_email, code, subject, title_text, description_text):
    """General function to send styled emails"""
    msg = MIMEMultipart("alternative")
    msg['Subject'] = f"{subject} - Recruit Finder AI"
    msg['From'] = f"Recruit Finder AI <{MAIL_USERNAME}>"
    msg['To'] = recipient_email

    text_content = f"{title_text}\n\nCode: {code}\n\n{description_text}"
    
    html_content = f"""
    <html>
        <body style="font-family: 'Segoe UI', Arial, sans-serif; line-height: 1.6; color: #333; background-color: #f4f4f7; padding: 40px;">
            <div style="max-width: 500px; margin: 0 auto; border: 1px solid #eaeaec; padding: 30px; border-radius: 12px; background-color: #ffffff; box-shadow: 0 4px 10px rgba(0,0,0,0.05);">
                <h2 style="color: #007bff; text-align: center; margin-bottom: 20px;">Recruit Finder AI</h2>
                <p style="font-size: 16px; font-weight: bold; text-align: center;">{title_text}</p>
                <p style="font-size: 15px; text-align: center;">{description_text}</p>
                <div style="text-align: center; margin: 30px 0;">
                    <span style="display: inline-block; padding: 15px 30px; background-color: #f8f9fa; border: 2px dashed #007bff; color: #007bff; font-size: 32px; font-weight: bold; letter-spacing: 8px; border-radius: 8px;">
                        {code}
                    </span>
                </div>
                <hr style="border: 0; border-top: 1px solid #eeeeee; margin: 30px 0;">
                <p style="font-size: 12px; color: #999; text-align: center;">
                    This is an automated message. Please do not reply to this email.
                </p>
            </div>
        </body>
    </html>
    """
    msg.attach(MIMEText(text_content, "plain"))
    msg.attach(MIMEText(html_content, "html"))

    with smtplib.SMTP(SMTP_SERVER, SMTP_PORT) as server:
        server.starttls()
        server.login(MAIL_USERNAME, MAIL_PASSWORD)
        server.sendmail(MAIL_USERNAME, recipient_email, msg.as_string())

@app.route('/send-reset-code', methods=['POST'])
def handle_email_request():
    incoming_key = request.headers.get('X-Email-Key')
    if SERVICE_KEY and incoming_key != SERVICE_KEY:
        return jsonify({"error": "Unauthorized"}), 401

    data = request.json
    email = data.get('email')
    
    reset_code = data.get('resetCode')
    confirm_code = data.get('confirmationCode')

    try:
        if reset_code:
            send_email_template(
                email, 
                reset_code, 
                "Password Reset Request", 
                "Reset your password", 
                "We received a request to reset your password. Use the code below to set a new one."
            )
            return jsonify({"status": "Success", "message": "Reset code sent"}), 200
            
        elif confirm_code:
            send_email_template(
                email, 
                confirm_code, 
                "Confirm Your Email", 
                "Welcome to Recruit Finder AI!", 
                "Thank you for registering. Please enter the code below to verify your account."
            )
            return jsonify({"status": "Success", "message": "Confirmation code sent"}), 200
        
        else:
            return jsonify({"error": "No code provided"}), 400

    except Exception as e:
        print(f"SMTP Error: {str(e)}")
        return jsonify({"error": str(e)}), 500

if __name__ == '__main__':
    port = int(os.getenv('EMAIL_SERVICE_PORT', 8001))
    app.run(host='127.0.0.1', port=port, debug=True, ssl_context='adhoc')