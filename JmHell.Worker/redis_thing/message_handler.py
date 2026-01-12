from abc import ABC, abstractmethod
from typing import TypeVar, Generic
from google.protobuf.message import Message

# Define a TypeVar that is bound to protobuf Message
T = TypeVar('T', bound=Message)


class MessageHandler(Generic[T], ABC):
    def __init__(self):
        pass

    @abstractmethod
    def handle_message(self, message: T) -> bool:
        pass
