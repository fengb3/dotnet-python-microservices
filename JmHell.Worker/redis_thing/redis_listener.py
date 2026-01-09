import os
import socket
import threading
from typing import Dict, Type

import redis
from google.protobuf.message import Message
from loguru import logger

# 关键：从 package 导入（要求 `JmHell.Worker/message_handlers/__init__.py` 存在并导出 get_all_handlers）
from message_handlers import get_all_handlers


class RedisListener:
    def __init__(self, host, port, password=None, ssl=False, ssl_cert_reqs=None, ssl_ca_certs=None):
        self.redis_client = redis.Redis(
            host=host,
            port=port,
            password=password,
            ssl=ssl,
            ssl_cert_reqs=ssl_cert_reqs,
            ssl_ca_certs=ssl_ca_certs,
            decode_responses=False,
        )

        # handlers: 映射 stream_name -> handler class
        self.handlers: Dict[str, Type] = get_all_handlers()
        if not self.handlers:
            logger.warning("Warning: No message handlers were discovered.")
        else:
            logger.info(f"Initialized listener with handlers for: {', '.join(self.handlers.keys())}")

    def _listen_loop(self, stream_name: str, handler_class: Type, group_name: str):
        consumer_name = f"{socket.gethostname()}-{os.getpid()}"
        handler_instance = handler_class()

        message_type = None
        for base in getattr(handler_class, "__orig_bases__", []):
            from typing import get_args

            args = get_args(base)
            if args:
                message_type = args[0]
                break

        if not message_type:
            logger.error(f"Could not determine message type for handler {handler_class.__name__}. Stopping thread.")
            return

        try:
            self.redis_client.xgroup_create(stream_name, group_name, id="0", mkstream=True)
        except redis.exceptions.ResponseError as e:
            if "BUSYGROUP" in str(e):
                logger.info(f"Consumer group '{group_name}' already exists for stream '{stream_name}'.")
            else:
                logger.error(f"Error creating consumer group for '{stream_name}': {e}")
                return

        logger.info(f"Thread started: Listening to stream '{stream_name}' with handler '{handler_class.__name__}'...")

        while True:
            try:
                messages = self.redis_client.xreadgroup(
                    group_name,
                    consumer_name,
                    {stream_name: ">"},
                    count=1,
                    block=5000,
                )
                if not messages:
                    continue

                for _, stream_messages in messages:
                    for message_id, message_data in stream_messages:
                        try:
                            raw = message_data.get(b"data")
                            if raw is None:
                                raise ValueError("Missing field `data` in stream entry")

                            proto_message: Message = message_type()
                            proto_message.ParseFromString(raw)

                            handler_instance.handle_message(proto_message)
                            self.redis_client.xack(stream_name, group_name, message_id)
                        except Exception as e:
                            logger.error(f"Error processing message {message_id} from stream '{stream_name}': {e}")

            except Exception as e:
                logger.error(f"An error occurred in listener for stream '{stream_name}': {e}")
                import time

                time.sleep(5)

    def start_listening(self):
        if not self.handlers:
            logger.warning("Cannot start listening, no handlers are registered.")
            return

        threads = []
        for message_type_name, handler_class in self.handlers.items():
            stream_name = message_type_name
            group_name = os.getenv("CONSUMER_GROUP", "api-consumers")

            thread = threading.Thread(
                target=self._listen_loop,
                args=(stream_name, handler_class, group_name),
                daemon=True,
            )
            threads.append(thread)
            thread.start()

        logger.info(f"Started {len(threads)} listener threads.")
        try:
            while True:
                import time

                time.sleep(1)
        except KeyboardInterrupt:
            logger.info("Shutting down listeners...")