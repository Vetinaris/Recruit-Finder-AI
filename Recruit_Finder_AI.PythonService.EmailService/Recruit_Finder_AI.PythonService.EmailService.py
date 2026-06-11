import os
import smtplib
import time
import json
import pika
from email.mime.text import MIMEText
from email.mime.multipart import MIMEMultipart
from dotenv import load_dotenv

load_dotenv()

SMTP_SERVER = os.getenv('MAIL_SERVER', 'smtp.gmail.com')
SMTP_PORT = int(os.getenv('MAIL_PORT', 587))
MAIL_USERNAME = os.getenv('MAIL_USERNAME')
MAIL_PASSWORD = os.getenv('MAIL_PASSWORD')
RABBITMQ_HOST = os.getenv("RABBITMQ_HOST", "rabbitmq")

def send_email_template(recipient_email, code, subject, title_text, description_text):
    """Generic function to send formatted emails via SMTP"""
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

    print(f" -> Attempting to send an email to {recipient_email} (Topic: {subject})...")
    with smtplib.SMTP(SMTP_SERVER, SMTP_PORT) as server:
        server.starttls()
        server.login(MAIL_USERNAME, MAIL_PASSWORD)
        server.sendmail(MAIL_USERNAME, recipient_email, msg.as_string())
    print(f" [OK] Email sent successfully to {recipient_email}")

def rabbitmq_callback(ch, method, properties, body):
    try:
        data = json.loads(body)
        email = data.get('email')
        reset_code = data.get('resetCode')
        confirm_code = data.get('confirmationCode')

        if not email:
            print(" [!] Error: Recipient's email address missing from message.")
            ch.basic_ack(delivery_tag=method.delivery_tag)
            return

        if reset_code:
            send_email_template(
                email, 
                reset_code, 
                "Password Reset Request", 
                "Reset your password", 
                "We received a request to reset your password. Use the code below to set a new one."
            )
        elif confirm_code:
            send_email_template(
                email, 
                confirm_code, 
                "Confirm Your Email", 
                "Welcome to Recruit Finder AI!", 
                "Thank you for registering. Please enter the code below to verify your account."
            )
        else:
            print(" [!] Error: Message received but no code (resetCode/confirmationCode) was transmitted.")

    except Exception as e:
        print(f" [CRITICAL] SMTP error while processing message: {e}")
    finally:
        ch.basic_ack(delivery_tag=method.delivery_tag)

def start_consumer():
    while True:
        try:
            connection = pika.BlockingConnection(pika.ConnectionParameters(host=RABBITMQ_HOST))
            channel = connection.channel()
            
            channel.queue_declare(queue='email_queue', durable=True)
            channel.basic_qos(prefetch_count=1)
            channel.basic_consume(queue='email_queue', on_message_callback=rabbitmq_callback)
            
            print(' [*] Email dispatch service started. Waiting for tasks in email_queue...')
            channel.start_consuming()
        except pika.exceptions.AMQPConnectionError:
            print(" [!] Could not connect to RabbitMQ. Retrying in 5 seconds....")
            time.sleep(5)

if __name__ == '__main__':
    start_consumer()