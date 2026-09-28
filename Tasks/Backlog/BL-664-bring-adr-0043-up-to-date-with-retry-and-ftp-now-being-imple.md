---
id: BL-664
title: Bring ADR-0043 up to date with --retry and FTP now being implemented
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-664 — Bring ADR-0043 up to date with --retry and FTP now being implemented

## Goal

ADR-0043 no longer states anything false about the code: its Decision and Consequences say what `num_retries`, `ftp_entry_path`, `tls_earlydata`, `ssl_verify_result` and the other fixed-value variables print today and why, pointing at the tasks and ADRs that changed or will change them.

## Context

- Conformance audit 2026-09-28, row 47 (Minor): ADR-0043 says `num_retries` prints 0 because `--retry` is not parsed, and `ftp_entry_path` prints nothing because the FTP handler is empty; `--retry` is parsed (`Curl.Core.UnitLibrary/TransferRetrier.cs`) and FTP works.
- Accepted ADRs are amended with a dated note (the README's status column records "Amended by" or "superseded by", as for ADR-0076 and ADR-0109); do not rewrite the original decision text. BL-513 (`num_retries`), BL-514 (`ftp_entry_path`) and BL-661 (`ssl_verify_result` off Windows) change these variables; state the current behaviour and name the task for each.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0043-w-variables-with-a-fixed-value-print-it-and-those-without-a-source-stay-unknown.md` has a dated amendment section stating, for each variable it lists, what Curl prints now and why, with no claim that `--retry` is unparsed or that the FTP handler is empty.
- [ ] Its row in `Documentation/Planning/Decisions/README.md` shows the amendment.
- [ ] Every statement in the amendment is checked against `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` as it is when the task runs.

## Notes

## Log

- 2026-09-28: Created.
