import redis
import threading
import os
import socket
from google.protobuf.message import Message
from message_handlers import get_all_handlers
from typing import Dict, Type

class RedisListener:
    def __init__(self, host, port, password=None, ssl=False, ssl_cert_reqs=None, ssl_ca_certs=None):
        # Ensure decode_responses is False to get bytes for protobuf
        self.redis_client = redis.Redis(
            host=host,
            port=port,
            password=password,
            ssl=ssl,
            ssl_cert_reqs=ssl_cert_reqs,
            ssl_ca_certs=ssl_ca_certs,
            decode_responses=False
        )
        self.handlers: Dict[str, Type] = get_all_handlers()
        if not self.handlers:
            print("Warning: No message handlers were discovered.")
        else:
            print(f"Initialized listener with handlers for: {', '.join(self.handlers.keys())}")

    def _listen_loop(self, stream_name: str, handler_class: Type, group_name: str):
        """A dedicated listening loop for a single stream and handler."""
        consumer_name = f"{socket.gethostname()}-{os.getpid()}"
        handler_instance = handler_class()

        # Get the message type class from the handler's generic definition
        message_type = None
        for base in getattr(handler_class, '__orig_bases__', []):
            from typing import get_args
            args = get_args(base)
            if args:
                message_type = args[0]
                break
        
        if not message_type:
            print(f"Could not determine message type for handler {handler_class.__name__}. Stopping thread.")
            return

        try:
            self.redis_client.xgroup_create(stream_name, group_name, id='0', mkstream=True)
        except redis.exceptions.ResponseError as e:
            if "BUSYGROUP" in str(e):
                print(f"Consumer group '{group_name}' already exists for stream '{stream_name}'.")
            else:
                print(f"Error creating consumer group for '{stream_name}': {e}")
                return

        print(f"Thread started: Listening to stream '{stream_name}' with handler '{handler_class.__name__}'...")

        while True:
            try:
                messages = self.redis_client.xreadgroup(group_name, consumer_name, {stream_name: '>'}, count=1, block=5000)
                if not messages:
                    continue

                for _, stream_messages in messages:
                    for message_id, message_data in stream_messages:
                        try:
                            # Create an instance of the correct message type and parse
                            proto_message = message_type()
                            proto_message.ParseFromString(message_data[b'data'])

                            # Process the message with the handler instance
                            handler_instance.handle_message(proto_message)

                            # Acknowledge the message
                            self.redis_client.xack(stream_name, group_name, message_id)
                        except Exception as e:
                            print(f"Error processing message {message_id} from stream '{stream_name}': {e}")

            except Exception as e:
                print(f"An error occurred in listener for stream '{stream_name}': {e}")
                # Avoid tight loop on persistent error
                import time
                time.sleep(5)

    def start_listening(self):
        """Starts a listener thread for each discovered handler."""
        if not self.handlers:
            print("Cannot start listening, no handlers are registered.", flush=True)
            return

        threads = []
        for message_type_name, handler_class in self.handlers.items():
            # Convention: stream name is the same as the message type name
            stream_name = message_type_name
            # Convention: group name is based on the stream name
            group_name = f"{stream_name}-group"
            
            thread = threading.Thread(
                target=self._listen_loop,
                args=(stream_name, handler_class, group_name),
                daemon=True  # Allows main thread to exit even if these threads are running
            )
            threads.append(thread)
            thread.start()

        print(f"Started {len(threads)} listener threads.")
        # Keep the main thread alive to allow daemon threads to run
        try:
            while True:
                # Or use thread.join() if you want to block until they finish
                import time
                time.sleep(1)
        except KeyboardInterrupt:
            print("\nShutting down listeners...")

