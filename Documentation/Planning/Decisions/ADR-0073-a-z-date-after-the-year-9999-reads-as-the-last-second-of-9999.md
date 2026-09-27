# ADR-0073 — A `-z` date after the year 9999 reads as the last second of 9999

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

libcurl 8.21.0's `parsedate` computes a `-z` date in a 64-bit `time_t`, so it accepts years far
beyond 9999: `curl -z "1 Jan 099999999" file:///...` prints no warning. `CurlDateParser` (BL-138)
returns a `DateTimeOffset`, which `TimeCondition.Value` carries and which stops at
9999-12-31 23:59:59.9999999 UTC, so it refused such a date and printed the illegal-date warning
(BL-247).

Measured with curl 8.21.0 (Windows, Schannel) on 2026-09-27 against a file written that day:

| `-z` value | Transfers? | Meaning |
| --- | --- | --- |
| `1 Jan 099999999` | no | accepted, file not new enough |
| `-1 Jan 099999999` | yes | accepted, file unmodified since |
| `31 Dec 9999 23:00 -1400` | no / yes with `-` | accepted, after 9999 once the zone is applied |
| `00000101` (year 0) | yes both ways | not a date: curl refuses years before 1583 |

## Decision

- `CurlDateParser` reads an instant after the last whole second a `DateTimeOffset` holds as that
  second, 9999-12-31 23:59:59 UTC. `TimeCondition` is unchanged.
- An instant before year 1 is still refused: curl refuses those years itself (the 1583 floor that
  BL-256 brings to `-z`).

## Consequences

- For `file://` (and any comparison against a real file or `Last-Modified` time) the clamped
  instant compares exactly as curl's does: every such time is earlier.
- The HTTP `If-Modified-Since` header for such a date says `Fri, 31 Dec 9999 23:59:59 GMT`; what
  curl sends there (or whether it fails formatting the date) is not measured here.
- No contract change in `Curl.Protocol.Abstractions.UnitLibrary`.

## Alternatives considered

- **Widen `TimeCondition` to Unix seconds (`long`).** Exact, but changes the shared contract every
  protocol reads and every handler's comparison, for dates no real file has.
- **Keep refusing.** Prints a warning curl does not and drops the condition.
