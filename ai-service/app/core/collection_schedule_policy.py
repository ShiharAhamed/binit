"""Deterministic start-time policy for advisory C2 collection proposals."""

from dataclasses import dataclass
from datetime import datetime, time, timedelta, tzinfo

from app.core.config import Settings, municipal_timezone


@dataclass(frozen=True)
class CollectionSchedulePolicy:
    timezone_name: str
    timezone: tzinfo
    window_start: time
    window_end: time

    @classmethod
    def from_settings(cls, settings: Settings) -> "CollectionSchedulePolicy":
        return cls(
            settings.MUNICIPAL_TIMEZONE,
            municipal_timezone(settings.MUNICIPAL_TIMEZONE),
            settings.COLLECTION_WINDOW_START,
            settings.COLLECTION_WINDOW_END,
        )

    def earliest(self, now: datetime) -> datetime:
        if now.tzinfo is None or now.utcoffset() is None:
            raise ValueError("Planning reference time must include timezone information")
        local_now = now.astimezone(self.timezone)
        local_time = local_now.time().replace(tzinfo=None)
        if local_time < self.window_start:
            return datetime.combine(local_now.date(), self.window_start, self.timezone)
        if local_time >= self.window_end:
            return datetime.combine(local_now.date() + timedelta(days=1), self.window_start, self.timezone)
        return local_now

    def validation_error(self, proposed: datetime, now: datetime) -> str | None:
        if proposed.tzinfo is None or proposed.utcoffset() is None:
            return "scheduledAt must include timezone information (use UTC 'Z' suffix)."
        if proposed < now:
            return "scheduledAt is before the planning reference timestamp. Proposed schedule must be in the future."
        local_proposed = proposed.astimezone(self.timezone)
        local_time = local_proposed.time().replace(tzinfo=None)
        if not self.window_start <= local_time < self.window_end:
            return (
                f"Proposed scheduled time {local_proposed.strftime('%H:%M')} local is outside the allowed "
                f"{self.window_start.strftime('%H:%M')}–{self.window_end.strftime('%H:%M')} "
                f"collection window ({self.timezone_name})."
            )
        if proposed < self.earliest(now):
            return "scheduledAt is before the earliest permissible collection time."
        return None
