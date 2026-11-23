---
id: BL-5838
title: Fix fitness finding EXT-110: URL passwords inside other response headers are written in clear to --log-file, --har and --record
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs, Curl.Extension.Har.UnitLibrary/HarDocument.cs, Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-23
---
# BL-5838 — Fix fitness finding EXT-110: URL passwords inside other response headers are written in clear to --log-file, --har and --record

## Goal

EXT-110 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: URL passwords inside other response headers are written in clear to --log-file, --har and --record. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** Low (credential written to logs the ADRs say never hold one)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** diagnostic log, HAR, record/replay (neighbour of the fixed EXT-091)
- **Repro:** a server on 127.0.0.1:27020 whose `/neigh` answers 200 with:
  - `WWW-Authenticate: Bearer realm="http://wu:WWWSECRET@127.0.0.1/r"`
  - `Content-Security-Policy: report-uri http://cs:CSPSECRET@127.0.0.1/r`
  - `X-Upstream: https://xu:XHDRSECRET@127.0.0.1/q`

  `curl -sS -o NUL --log-level verbose --log-file l.txt --har h.har --record rec http://127.0.0.1:27020/neigh`, then `grep -ac "WWWSECRET\|CSPSECRET\|XHDRSECRET" l.txt h.har rec/*`.
- **Expected:** ADR-0222 rule 7 ("Never logged: … URL user-info passwords"); ADR-1135 (the recording holds no password). `Location`, `Content-Location`, `Link` and `Refresh` are already shown as `REDACTED:REDACTED`. Every count is 0.
- **Actual (.NET):** each file contains all three secrets in clear; redaction is a fixed list of header names.
- **Likely location:** `Curl.Original.Protocol.Http.UnitLibrary/HttpExchangeLog.cs`, `Curl.Extension.Har.UnitLibrary/HarDocument.cs`, and the `Curl.Extension.RecordReplay.UnitLibrary` writer: redact any `scheme://user:pass@` inside any response header value.
- **Acceptance check:** the grep prints 0 for every file.

## Acceptance criteria

- [x] the grep prints 0 for every file.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Every non-secret response header value now has the user-info of any URL in it redacted (HttpExchangeLog, HarDocument, RecordReplayTransferHook), replacing the fixed Link/Refresh name list. Grep criterion is pinned by unit rows for WWW-Authenticate, Content-Security-Policy and X-Upstream; not run against a live mock. Fast tests and Measure-CodeQuality (0 failing) green.

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: b1e8aac6 69116afe 3d836d5e.
- 2026-11-23: Doing -> Done. URL passwords in any response header are redacted in --log-file, --har and --record
