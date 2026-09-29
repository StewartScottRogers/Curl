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
completed: 2026-09-29
---
# BL-664 — Bring ADR-0043 up to date with --retry and FTP now being implemented

## Goal

ADR-0043 no longer states anything false about the code: its Decision and Consequences say what `num_retries`, `ftp_entry_path`, `tls_earlydata`, `ssl_verify_result` and the other fixed-value variables print today and why, pointing at the tasks and ADRs that changed or will change them.

## Context

- Conformance audit 2026-09-28, row 47 (Minor): ADR-0043 says `num_retries` prints 0 because `--retry` is not parsed, and `ftp_entry_path` prints nothing because the FTP handler is empty; `--retry` is parsed (`Curl.Core.UnitLibrary/TransferRetrier.cs`) and FTP works.
- Accepted ADRs are amended with a dated note (the README's status column records "Amended by" or "superseded by", as for ADR-0076 and ADR-0109); do not rewrite the original decision text. BL-513 (`num_retries`), BL-514 (`ftp_entry_path`) and BL-661 (`ssl_verify_result` off Windows) change these variables; state the current behaviour and name the task for each.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-0043-w-variables-with-a-fixed-value-print-it-and-those-without-a-source-stay-unknown.md` has a dated amendment section stating, for each variable it lists, what Curl prints now and why, with no claim that `--retry` is unparsed or that the FTP handler is empty.
- [x] Its row in `Documentation/Planning/Decisions/README.md` shows the amendment.
- [x] Every statement in the amendment is checked against `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` as it is when the task runs.

## Notes

- Did the docs pipeline in-session rather than through align-and-document: two Markdown edits, no code.
- Amendment appended as a dated section (ADR-0040 style); the original Decision text is untouched. README status column reads "Accepted; amended 2026-09-29 by BL-664 ...", following the "Accepted; ... superseded by" pattern.
- Checked against `TransferWriteOutVariables.cs` on 2026-09-29: `ssl_verify_result`, `proxy_ssl_verify_result` and `tls_earlydata` are still constant 0; `num_retries` is `RetryCount` (set in `CurlCommandRunner` from `TransferRetrier`); `ftp_entry_path` is `TransferReport.FtpEntryPath` (set by `FtpSession` from PWD).
- `--tls-earlydata` is now parsed but no provider applies it (BL-710), so 0 stays true. No task covered reporting early-data bytes, so filed BL-905 (depends on BL-710).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0043 amended with what each fixed and sourced -w variable prints now; BL-905 filed
