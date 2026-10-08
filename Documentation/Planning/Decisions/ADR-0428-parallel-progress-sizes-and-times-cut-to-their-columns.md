# ADR-0428 — A `-Z` progress size is cut to five characters and a time to eight, as curl's buffers cut them

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1657
- Decided by Claude under Stewart's delegation.

## Context

`ParallelProgressMeterText.Size` reproduces curl 8.21.0's `max5data` and `Time` its
`time2str` (`src/tool_progress.c`). Two inputs overflowed their columns:

- `max5data` writes the tenths digit as `bytes % unit / (unit / 10)`. `unit / 10` rounds
  down, so for the last few bytes below 100 of a unit the digit is 10: 100 MiB less one byte
  formats as `99.10M`, six characters.
- `time2str` writes days alone as `%7d` then `d`; past 9,999,999 days that is longer than
  eight characters (`long.MaxValue` seconds is `106751991167300d`).

curl computes the same digits but writes them with `msnprintf(max5, 6, ...)` and
`msnprintf(r, 9, ...)`: a six-byte and a nine-byte buffer, each holding one byte for the
terminating zero, so curl's text is cut to five and eight characters.

## Decision

`Size` cuts its `xx.yU` text to five characters and `Time` cuts its days-alone text to eight,
exactly as the buffers do: 100 MiB less one byte is `99.10` (the unit letter cut off), and
`long.MaxValue` seconds is `10675199`. The other layouts never exceed their columns, so they
are left as they are.

## Why

- Matching the platform's curl byte for byte is the drop-in rule; curl's answer here is the
  cut text, odd as it looks, and a progress line that grows past its columns would shift every
  field after it.
- The cut follows from the buffer sizes in the source, so no transfer had to be measured: a
  `-Z` download of just under 100 MiB showing the final line would only confirm what
  `msnprintf`'s documented truncation already fixes.
