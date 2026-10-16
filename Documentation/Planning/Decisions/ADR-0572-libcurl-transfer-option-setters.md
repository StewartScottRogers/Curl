# ADR-0572 — `Libcurl`'s transfer option setters check values as curl 8.22.0's `setopt.c` does, and each `_LARGE` or `_MS` alias sets its option's field

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2401.

## Context

BL-2401 adds a setter row (ADR-0560) for every `CURLOPT_*` that `EasyTransferOptions` holds. The
record has one field per option: `RESUME_FROM` and `_LARGE`, `INFILESIZE` and `_LARGE`,
`MAXFILESIZE` and `_LARGE`, `TIMEVALUE` and `_LARGE` share a `long`, and `TIMEOUT` and `_MS` share a
`TimeSpan`.

## Decision

1. The rows live in `Libcurl/Options/Transfer`. `EasyOptionSetters.AddTransferSetters` calls
   `EasyTransferOptionSetters.AddTo`, one named static method per row, as ADR-0568 does for the
   network options. An alias is the same method on a second row, so the two cannot drift.
2. Range checks follow `setopt.c`, answering `BadFunctionArgument` and leaving the options unchanged:
   `RESUME_FROM(_LARGE)` and `INFILESIZE(_LARGE)` below -1; `MAXFILESIZE(_LARGE)`,
   `LOW_SPEED_LIMIT`, `MAX_SEND_SPEED_LARGE` and `MAX_RECV_SPEED_LARGE` below 0; `TIMECONDITION`
   outside `CURL_TIMECOND_NONE`..`_LASTMOD`; `PROXY_TRANSFER_MODE` other than 0 or 1; a negative
   `LOW_SPEED_TIME` or `TIMEOUT(_MS)`.
3. Times are cut to `INT_MAX` of their unit, as ADR-0568 does, so no `TimeSpan` overflows.
4. The buffer sizes are never an error, as in `setopt.c`: `BUFFERSIZE` below 1 is the 16384-byte
   default, below 1024 is raised to 1024 and past 10 MB is cut to it; `UPLOAD_BUFFERSIZE` is held
   between 16384 bytes and 2 MB.
5. `TIMEVALUE(_LARGE)` takes any `time_t`, before 1970 or past 9999 included. The on/off options
   (`FILETIME`, `DIRLISTONLY`, `NOBODY`, `UPLOAD`, `TRANSFERTEXT`, `CRLF`) are on for any value but 0.
   `RANGE` and `CUSTOMREQUEST` are stored as given, and `null` clears them.

## Consequences

- `NOBODY` and `UPLOAD` only set their fields; the request method they imply (`HEAD`, `PUT`) is the
  engine's to derive, as the record's doc comments say.
