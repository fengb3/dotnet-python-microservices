from redis_thing.message_handler import MessageHandler
from messages_pb2 import TaskMessage
import time
from loguru import logger


class TaskMessageHandler(MessageHandler[TaskMessage]):
    
    def handle_message(self, message: TaskMessage) -> bool:
        """Processes a TaskMessage."""
        logger.info(f"Handling TaskMessage with ID: {message.task_id}")
        logger.info(f"Task Type: {message.task_type}")
        logger.info(f"Task Data: {message.data}")

        # Simulate work
        time.sleep(1)

        logger.info(f"Finished processing task {message.task_id}")
        return True
        
