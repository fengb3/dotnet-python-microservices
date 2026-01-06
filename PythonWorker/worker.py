import os
import time
import redis
import messages_pb2
import socket
from datetime import datetime
from urllib.parse import urlparse

# Redis configuration
# When using Aspire, connection string is provided via environment variable
redis_connection_string = os.getenv('ConnectionStrings__redis', '')
if redis_connection_string:
    # Parse the connection string (format: host:port or redis://host:port)
    if redis_connection_string.startswith('redis://'):
        parsed = urlparse(redis_connection_string)
        REDIS_HOST = parsed.hostname or 'localhost'
        REDIS_PORT = parsed.port or 6379
    else:
        parts = redis_connection_string.split(':')
        REDIS_HOST = parts[0] if len(parts) > 0 else 'localhost'
        REDIS_PORT = int(parts[1]) if len(parts) > 1 else 6379
else:
    REDIS_HOST = os.getenv('REDIS_HOST', 'localhost')
    REDIS_PORT = int(os.getenv('REDIS_PORT', '6379'))

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
    r = redis.Redis(host=REDIS_HOST, port=REDIS_PORT, decode_responses=False)
    
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
