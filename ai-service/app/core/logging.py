import logging
import sys


def setup_logging(app_env: str = "development") -> logging.Logger:
    """Configures standard library logging suitable for development and startup."""
    log_level = logging.DEBUG if app_env.lower() == "development" else logging.INFO

    logging.basicConfig(
        level=log_level,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
        handlers=[logging.StreamHandler(sys.stdout)],
        force=True,
    )

    logger = logging.getLogger("smartwaste.ai")
    logger.setLevel(log_level)
    return logger
