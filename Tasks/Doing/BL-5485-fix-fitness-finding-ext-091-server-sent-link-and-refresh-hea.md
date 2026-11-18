---
id: BL-5485
title: Fix fitness finding EXT-091: Server-sent Link and Refresh headers with URL passwords are written in clear to --log-file, --har and --record
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs, Curl.Extension.Har.UnitLibrary/HarDocument.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5485 — Fix fitness finding EXT-091: Server-sent Link and Refresh headers with URL passwords are written in clear to --log-file, --har and --record

## Goal

EXT-091 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Server-sent Link and Refresh headers with URL passwords are written in clear to --log-file, --har and --record. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (credential in a log the ADR says never holds one)
- **Confidence:** Confirmed (exact repro run twice by the orchestrator on Windows; log case also seen once on Linux)
- **Protocol/area:** `--log-file`, `--har`, `--record` (Windows and Linux)
- **Repro:** an HTTP server on 127.0.0.1:45020 whose `/links` answers 200 with `Link: <http://lu:LINKSECRET@127.0.0.1:45020/a>; rel="next"`, `Refresh: 5; url=http://fu:REFRESHSECRET@127.0.0.1:45020/b` and body `hello`.
  - `curl -sS -o NUL --log-level verbose --log-file l.txt --har h.har --record rec http://127.0.0.1:45020/links`
  - `grep -ac 'LINKSECRET\|REFRESHSECRET' l.txt h.har rec/*`
- **Expected:** ADR-0222 rule 7: "Never logged: … URL user-info passwords"; ADR-1146 and ADR-1135 redact URL user info in the HAR and recording. Every count is 0 (values shown as `REDACTED:REDACTED`).
- **Actual (PORT):** each file contains both secrets in clear.
- **Likely location:** the URL-bearing header list in `Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs`, `Curl.Extension.Har.UnitLibrary/HarDocument.cs` and the `Curl.Extension.RecordReplay.UnitLibrary` writer: add `Link` and `Refresh` (redact each URL inside them).
- **Acceptance check:** the grep prints 0 for all files.

## Acceptance criteria

- [ ] the grep prints 0 for all files.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-091 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 81e4bf08 69116afe 3d836d5e.
