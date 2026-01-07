import os
import time
import redis
import messages_pb2
import socket
import ssl
from datetime import datetime
from urllib.parse import urlparse


def _parse_bool(value: str) -> bool:
    return str(value).strip().lower() in {"1", "true", "yes", "y", "on"}


def _parse_ssl_cert_reqs(value: str | None) -> int | None:
    if value is None:
        return None
    normalized = str(value).strip().lower()
    if normalized in {"none", "false", "0", "no"}:
        return ssl.CERT_NONE
    if normalized in {"optional"}:
        return ssl.CERT_OPTIONAL
    if normalized in {"required", "true", "1", "yes"}:
        return ssl.CERT_REQUIRED
    return None


def parse_redis_connection_string(connection_string: str):
    if not connection_string:
        return "localhost", 6379, None, False, None, None

    # Supported formats:
    # - redis://[:password@]host:port[/db]
    # - rediss://[:password@]host:port[/db]
    # - host:port
    # - host:port,password=...,ssl=true
    if connection_string.startswith("redis://") or connection_string.startswith("rediss://"):
        parsed = urlparse(connection_string)
        host = parsed.hostname or "localhost"
        port = parsed.port or 6379
        password = parsed.password
        use_ssl = parsed.scheme == "rediss"
        return host, port, password, use_ssl, None, None

    # Flat connection string
    host_port, *option_parts = connection_string.split(",")
    if ":" in host_port:
        host, port_part = host_port.split(":", 1)
    else:
        host, port_part = host_port, "6379"

    # Some providers append options right after the port (e.g. host:port,password=...)
    port_str = port_part.split(",", 1)[0].strip()
    port = int(port_str) if port_str else 6379

    options = {}
    for part in option_parts:
        if not part or "=" not in part:
            continue
        key, value = part.split("=", 1)
        options[key.strip().lower()] = value.strip()

    password = options.get("password")
    use_ssl = _parse_bool(options.get("ssl", "false"))
    ssl_cert_reqs = _parse_ssl_cert_reqs(options.get("ssl_cert_reqs"))
    ssl_ca_certs = options.get("ssl_ca_certs")
    return host or "localhost", port, password, use_ssl, ssl_cert_reqs, ssl_ca_certs

# Redis configuration
# When using Aspire, connection string is provided via environment variable
redis_connection_string = os.getenv('ConnectionStrings__redis', '')
if redis_connection_string:
    (
        REDIS_HOST,
        REDIS_PORT,
        REDIS_PASSWORD,
        REDIS_SSL,
        REDIS_SSL_CERT_REQS,
        REDIS_SSL_CA_CERTS,
    ) = parse_redis_connection_string(redis_connection_string)
else:
    REDIS_HOST = os.getenv('REDIS_HOST', 'localhost')
    REDIS_PORT = int(os.getenv('REDIS_PORT', '6379'))
    REDIS_PASSWORD = os.getenv('REDIS_PASSWORD')
    REDIS_SSL = _parse_bool(os.getenv('REDIS_SSL', 'false'))
    REDIS_SSL_CERT_REQS = _parse_ssl_cert_reqs(os.getenv('REDIS_SSL_CERT_REQS'))
    REDIS_SSL_CA_CERTS = os.getenv('REDIS_SSL_CA_CERTS')

# For local dev (Aspire often uses self-signed TLS), default to disabling cert verification
# when connecting to a local endpoint unless the user explicitly configured verification.
if REDIS_SSL and REDIS_SSL_CERT_REQS is None and (REDIS_HOST in {"localhost", "127.0.0.1", "::1"}):
    REDIS_SSL_CERT_REQS = ssl.CERT_NONE

TASK_STREAM = 'tasks'
RESULT_STREAM = 'results'
CONSUMER_GROUP = 'python-workers'
# Use hostname and process ID to create unique consumer name for scaling
CONSUMER_NAME = os.getenv('CONSUMER_NAME', f'worker-{socket.gethostname()}-{os.getpid()}')

def process_task(task_msg):
    """Process the task and return a result"""
    print(f"Processing task {task_msg.task_id} of type {task_msg.task_type}")
    print(f"Task data: {task_msg.data}")
    
    # Simulate some work
    time.sleep(2)
    
    # Create result
    result = f"Processed: {task_msg.data.upper()}"
    return result

def main():
    print(f"Python Worker starting...")
    print(f"Connecting to Redis at {REDIS_HOST}:{REDIS_PORT}")
    
    # Connect to Redis
    r = redis.Redis(
        host=REDIS_HOST,
        port=REDIS_PORT,
        password=REDIS_PASSWORD,
        ssl=REDIS_SSL,
        ssl_cert_reqs=REDIS_SSL_CERT_REQS,
        ssl_ca_certs=REDIS_SSL_CA_CERTS,
        decode_responses=False,
    )
    
    # Test connection
    try:
        r.ping()
        print("Connected to Redis successfully!")
    except redis.exceptions.ConnectionError as e:
        print(f"Failed to connect to Redis: {e}")
        return
    
    # Create consumer group (ignore if already exists)
    try:
        r.xgroup_create(TASK_STREAM, CONSUMER_GROUP, id='0', mkstream=True)
        print(f"Created consumer group '{CONSUMER_GROUP}'")
    except redis.exceptions.ResponseError as e:
        if "BUSYGROUP" not in str(e):
            print(f"Error creating consumer group: {e}")
        else:
            print(f"Consumer group '{CONSUMER_GROUP}' already exists")
    
    print(f"Listening for tasks on stream '{TASK_STREAM}'...")
    
    # Main processing loop
    while True:
        try:
            # Read from stream
            messages = r.xreadgroup(
                CONSUMER_GROUP, 
                CONSUMER_NAME, 
                {TASK_STREAM: '>'}, 
                count=1, 
                block=5000  # 5 second timeout
            )
            
            if not messages:
                continue
            
            for stream_name, stream_messages in messages:
                for message_id, message_data in stream_messages:
                    try:
                        # Deserialize the task message
                        task_msg = messages_pb2.TaskMessage()
                        task_msg.ParseFromString(message_data[b'data'])
                        
                        # Process the task
                        result_text = process_task(task_msg)
                        
                        # Create result message
                        result_msg = messages_pb2.ResultMessage()
                        result_msg.task_id = task_msg.task_id
                        result_msg.status = "completed"
                        result_msg.result = result_text
                        result_msg.timestamp = int(time.time() * 1000)
                        
                        # Publish result to results stream
                        r.xadd(RESULT_STREAM, {'data': result_msg.SerializeToString()})
                        print(f"Published result for task {task_msg.task_id}")
                        
                        # Acknowledge the message
                        r.xack(TASK_STREAM, CONSUMER_GROUP, message_id)
                        
                    except Exception as e:
                        print(f"Error processing message {message_id}: {e}")
                        
        except KeyboardInterrupt:
            print("\nShutting down worker...")
            break
        except Exception as e:
            print(f"Error in main loop: {e}")
            time.sleep(5)

if __name__ == "__main__":
    main()
