---
id: BL-5489
title: Fix fitness finding EXT-095: --record <dir> that cannot be written records nothing, with no warning and exit 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5489 — Fix fitness finding EXT-095: --record <dir> that cannot be written records nothing, with no warning and exit 0

## Goal

EXT-095 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: --record <dir> that cannot be written records nothing, with no warning and exit 0. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (silent loss of the recording; a later `--replay` fails)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** record-replay extension
- **Repro:** any HTTP server on 127.0.0.1:47060.
  1. `echo x > recfile; curl -sS -o NUL --record recfile http://127.0.0.1:47060/x; echo $?` (also `--record "<>bad"`)
  2. `curl -sS --replay recfile http://127.0.0.1:47060/x`
  3. Control: `curl -sS -o NUL --har recfile/x.har http://127.0.0.1:47060/x` → `curl: (23) Failed writing the HAR file recfile/x.har`, exit 23.
- **Expected:** `--extended-help`: "Records HTTP exchanges to a directory". When it cannot, the user is told, consistent with `--har` (exit 23) or the `Warning: Failed to open …` style of `--log-file`.
- **Actual (PORT):** step 1 exits 0 with no output; step 2 gives `curl: (7) No recorded exchange for GET http://127.0.0.1:47060/x in recfile`.
- **Likely location:** `Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs` `TryRecordAsync` (`catch (IOException) { }`).
- **Acceptance check:** step 1 prints a warning or error naming `--record` and `recfile` (exit 23, or exit 0 with a `Warning:` line).

## Acceptance criteria

- [ ] step 1 prints a warning or error naming `--record` and `recfile` (exit 23, or exit 0 with a `Warning:` line).
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-095 (Low).