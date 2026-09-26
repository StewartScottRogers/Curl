---
id: BL-013
title: Implement --max-filesize and shared range parsing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-007]
requirement: none
created: 2026-09-25
completed:
---
# BL-013 — Implement `--max-filesize`, plus range parsing shared across protocols

## Goal

`--max-filesize` works, and `-r`/`--range` is parsed once into the `ByteRange` type
from ADR-0003 so every protocol handler receives an already-validated range.

## Context

A Core concern: parsing happens once, and handlers read `ITransferContext.Range`
instead of parsing it themselves. See
`Documentation/Planning/Decisions/ADR-0003-itransfercontext-carries-transfer-options.md`.
Exit codes: https://curl.se/libcurl/c/libcurl-errors.html, checked against
curl 8.21.0.

## Acceptance criteria

- [ ] `-r`/`--range` is parsed into `ByteRange` in one place, and handlers receive it
      through `ITransferContext.Range`.
- [ ] A range that cannot be satisfied returns exit 33 (`CURLE_RANGE_ERROR`).
- [ ] `--max-filesize` is implemented, with the exit code upstream curl returns when
      it is exceeded.
- [ ] `-r 3-1`, `-r abc` and `-r -0` each return exit 33 (`CurlExitCode.RangeError`)
      with the message `Requested range was not delivered by the server`, as measured
      against curl 8.21.0. `ByteRange.Bounded` today throws
      `ArgumentOutOfRangeException` for a reversed range, and `ByteRange.Suffix` throws
      for a zero suffix, and nothing maps either exception to exit 33: the parser rejects
      these before a `ByteRange` is constructed, and a test asserts each of the three.
- [ ] `-r` together with `-C`/`--continue-at` is refused before any transfer, with exit 2
      (`CurlExitCode.FailedInit`) and the message
      `curl: --continue-at is mutually exclusive with --range`. Upstream makes them
      mutually exclusive — "This command line option is mutually exclusive with --range"
      (<https://curl.se/docs/manpage.html>, curl 8.21.0) — so
      `FileProtocolHandler.TryResolveWindow`'s current "`-C` wins over `-r`" precedence
      describes a case curl never reaches. A test pins the refusal.

## Notes

Measured against curl 8.21.0 on 2026-09-25: the `-C` with `-r` refusal prints **three**
stderr lines, not one, and exits 2:

```
curl: --continue-at is mutually exclusive with --range
curl: option -C: is badly used here
curl: try 'curl --help' or 'curl --manual' for more information
```


The original item did not name `--max-filesize`'s exit code. Confirm it from the
upstream error list during planning.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-25: Amended from the file:// conformance audit against curl 8.21.0: added the
  `-r 3-1`, `-r abc` and `-r -0` exit 33 cases and the `-r` with `-C` exit 2 refusal.
- 2026-09-25: Recorded all three stderr lines of the -C-with-r refusal; the criterion named only the first.
