from redis_thing.message_handler import MessageHandler
from messages_pb2 import TaskMessage
import time


class TaskMessageHandler(MessageHandler[TaskMessage]):
    def handle_message(self, message: TaskMessage):
        """Processes a TaskMessage."""
        print(f"Handling TaskMessage with ID: {message.task_id}")
        print(f"Task Type: {message.task_type}")
        print(f"Task Data: {message.data}")

        # Simulate work
        time.sleep(1)

        print(f"Finished processing task {message.task_id}")
        
