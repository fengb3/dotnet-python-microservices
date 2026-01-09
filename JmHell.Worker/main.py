import os
import ssl
from urllib.parse import urlparse
from redis_thing.redis_listener import RedisListener
from loguru import logger


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

    if connection_string.startswith("redis://") or connection_string.startswith(
        "rediss://"
    ):
        parsed = urlparse(connection_string)
        host = parsed.hostname or "localhost"
        port = parsed.port or 6379
        password = parsed.password
        use_ssl = parsed.scheme == "rediss"
        return host, port, password, use_ssl, None, None

    host_port, *option_parts = connection_string.split(",")
    if ":" in host_port:
        host, port_part = host_port.split(":", 1)
    else:
        host, port_part = host_port, "6379"

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


def main():
    """
    Main entry point for the Python worker.
    Initializes and starts the Redis listener, which dynamically discovers
    and manages all message handlers.
    """
    logger.info("Initializing Python Worker...")

    redis_connection_string = os.getenv('ConnectionStrings__redis', '')
    if redis_connection_string:
        (
            host,
            port,
            password,
            use_ssl,
            ssl_cert_reqs,
            ssl_ca_certs,
        ) = parse_redis_connection_string(redis_connection_string)
    else:
        host = os.getenv('REDIS_HOST', 'localhost')
        port = int(os.getenv('REDIS_PORT', '6379'))
        password = os.getenv('REDIS_PASSWORD')
        use_ssl = _parse_bool(os.getenv('REDIS_SSL', 'false'))
        ssl_cert_reqs = _parse_ssl_cert_reqs(os.getenv('REDIS_SSL_CERT_REQS'))
        ssl_ca_certs = os.getenv('REDIS_SSL_CA_CERTS')

    if use_ssl and ssl_cert_reqs is None and (host in {"localhost", "127.0.0.1", "::1"}):
        ssl_cert_reqs = ssl.CERT_NONE

    logger.info(f"Attempting to connect to Redis at {host}:{port}")

    listener = RedisListener(
        host=host,
        port=port,
        password=password,
        ssl=use_ssl,
        ssl_cert_reqs=ssl_cert_reqs,
        ssl_ca_certs=ssl_ca_certs,
    )
    
    listener.start_listening()


if __name__ == "__main__":
    main()

