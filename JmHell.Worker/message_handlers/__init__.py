import pkgutil
import importlib
import inspect
from typing import get_args
from redis_thing.message_handler import MessageHandler

# Dictionary to store discovered handlers: {message_type_name: handler_class}
_discovered_handlers = {}


def _discover_handlers():
    """
    Discovers all MessageHandler subclasses in this package,
    and populates the _discovered_handlers dictionary.
    The key is the name of the message type.
    """
    # Avoid re-discovering if already populated
    if _discovered_handlers:
        return

    package_path = __path__
    package_name = __name__

    # Iterate over all modules in the current package
    for _, module_name, _ in pkgutil.walk_packages(package_path, prefix=f"{package_name}."):
        print(f"Inspecting module: {module_name}")
        try:
            # Import the module
            module = importlib.import_module(module_name)
            # Inspect the module for classes
            for _, obj in inspect.getmembers(module, inspect.isclass):
                # Check if the class is a subclass of MessageHandler but not MessageHandler itself
                if issubclass(obj, MessageHandler) and obj is not MessageHandler:
                    # Find the generic type from the base class
                    # obj.__orig_bases__ will contain the generic form, e.g., MessageHandler[TaskMessage]
                    for base in getattr(obj, '__orig_bases__', []):
                        args = get_args(base)
                        if args:
                            message_type = args[0]
                            type_name = message_type.DESCRIPTOR.name
                            if type_name in _discovered_handlers:
                                print(f"Warning: Duplicate handler for message type {type_name}. Overwriting.")
                            # Store the class itself
                            _discovered_handlers[type_name] = obj
                            print(f"Discovered handler '{obj.__name__}' for message type '{type_name}'")
                            break  # Found the message type, move to the next class
        except Exception as e:
            print(f"Error during handler discovery in module {module_name}: {e}")


def get_handler_for_message_type(message_type_name: str):
    """
    Returns the handler class for a given message type name.
    """
    return _discovered_handlers.get(message_type_name)


def get_all_handlers():
    """
    Returns a dictionary of all discovered message type names and their handler classes.
    """
    return _discovered_handlers.copy()


# Automatically discover handlers when the package is imported.
print("Discovering message handlers...")
_discover_handlers()
print(f"Found {_discovered_handlers.__len__()} handlers.")

