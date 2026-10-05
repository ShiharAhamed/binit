from datetime import datetime, timedelta, timezone

import pytest
from pydantic import ValidationError

from app.core.collection_schedule_policy import CollectionSchedulePolicy
from app.core.config import Settings


COLOMBO = timezone(timedelta(hours=5, minutes=30))
POLICY = CollectionSchedulePolicy.from_settings(Settings(_env_file=None))


def local(day: int, hour: int, minute: int = 0) -> datetime:
    return datetime(2026, 10, day, hour, minute, tzinfo=COLOMBO)


@pytest.mark.parametrize("now, earliest", [
    (local(5, 22), local(6, 8)),
    (local(5, 6, 30), local(5, 8)),
    (local(5, 10, 30), local(5, 10, 30)),
    (local(5, 15, 45), local(5, 15, 45)),
    (local(5, 16), local(6, 8)),
])
def test_earliest_local_collection_start(now, earliest):
    assert POLICY.earliest(now) == earliest


@pytest.mark.parametrize("now, proposed, valid", [
    (local(5, 22), local(6, 0, 30), False),
    (local(5, 22), local(6, 2, 30), False),
    (local(5, 22), local(6, 7, 59), False),
    (local(5, 22), local(6, 8), True),
    (local(5, 22), local(6, 9, 30), True),
    (local(5, 6, 30), local(5, 7, 30), False),
    (local(5, 6, 30), local(5, 8), True),
    (local(5, 6, 30), local(5, 10), True),
    (local(5, 10, 30), local(5, 8), False),
    (local(5, 10, 30), local(5, 10), False),
    (local(5, 10, 30), local(5, 10, 31), True),
    (local(5, 10, 30), local(5, 11), True),
    (local(5, 10, 30), local(5, 15, 30), True),
    (local(5, 10, 30), local(5, 16), False),
    (local(5, 16), local(5, 16), False),
    (local(5, 16), local(5, 16, 30), False),
    (local(5, 16), local(5, 23), False),
    (local(5, 16), local(6, 8), True),
    (local(5, 15, 45), local(5, 15, 50), True),
    (local(5, 15, 45), local(5, 16), False),
    (local(5, 10, 30), local(5, 7, 59), False),
])
def test_local_collection_window(now, proposed, valid):
    assert (POLICY.validation_error(proposed, now) is None) is valid


def test_utc_timestamp_is_checked_in_colombo_time():
    now = local(5, 22)
    assert POLICY.validation_error(datetime(2026, 10, 6, 2, 30, tzinfo=timezone.utc), now) is None
    assert POLICY.validation_error(datetime(2026, 10, 5, 21, tzinfo=timezone.utc), now) is not None


def test_naive_proposal_is_rejected():
    assert "timezone information" in POLICY.validation_error(datetime(2026, 10, 6, 8), local(5, 22))


def test_window_and_timezone_configuration_fail_fast():
    settings = Settings(_env_file=None, MUNICIPAL_TIMEZONE="Asia/Colombo",
                        COLLECTION_WINDOW_START="09:00", COLLECTION_WINDOW_END="17:00")
    policy = CollectionSchedulePolicy.from_settings(settings)
    assert policy.window_start.hour == 9 and policy.window_end.hour == 17
    for values in [
        {"COLLECTION_WINDOW_START": "16:00", "COLLECTION_WINDOW_END": "08:00"},
        {"COLLECTION_WINDOW_START": "08:00", "COLLECTION_WINDOW_END": "08:00"},
        {"COLLECTION_WINDOW_START": "wrong"},
        {"COLLECTION_WINDOW_START": "08:00Z"},
        {"MUNICIPAL_TIMEZONE": "Invalid/Zone"},
    ]:
        with pytest.raises(ValidationError):
            Settings(_env_file=None, **values)
