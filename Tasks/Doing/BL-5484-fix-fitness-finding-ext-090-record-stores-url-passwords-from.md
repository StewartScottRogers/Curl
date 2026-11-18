---
id: BL-5484
title: Fix fitness finding EXT-090: --record stores URL passwords from response Location and Content-Location headers in clear
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.RecordReplay.UnitLibrary/RecordedExchangeEncoding.cs, Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5484 — Fix fitness finding EXT-090: --record stores URL passwords from response Location and Content-Location headers in clear

## Goal

EXT-090 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: --record stores URL passwords from response Location and Content-Location headers in clear. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (a credential in a file the ADR says holds no password)
- **Confidence:** Confirmed (Windows 2 runs, Linux 1 run)
- **Protocol/area:** `--record` (record-replay), Windows and Linux
- **Repro:** an HTTP server on 127.0.0.1:45020: `/redir` answers 302 with `Location: http://u:LOCSECRET8@127.0.0.1:45020/plain`, `Content-Location: http://cu:CLSECRET9@127.0.0.1:45020/x`, `Content-Length: 0`; `/plain` answers 200 `hello\n`.
  - `curl -sS -m5 --record rec -L http://127.0.0.1:45020/redir -o NUL`
  - `grep -ac 'LOCSECRET8\|CLSECRET9' rec/*`
- **Expected:** ADR-1135 (BL-5323) redacts user info to `REDACTED:REDACTED` "so the recording holds no password"; `--har` and `--log-file` already redact `Location` and `Content-Location` (BL-5379). Replay keys the next hop on the redacted URL, so redaction does not break `--replay -L`.
- **Actual (PORT):** exit 0; the recorded head of the `/redir` exchange contains `Location: http://u:LOCSECRET8@…` and `Content-Location: http://cu:CLSECRET9@…` (grep counts 2). An IPv6 `Location` (`http://u6:V6SECRET@[::1]:…`) is also stored in clear.
- **Likely location:** the recording writer in `Curl.Extension.RecordReplay.UnitLibrary`, which writes the head bytes; apply the user-info redaction to `Location`, `Content-Location`, `Link` and `Refresh` lines.
- **Acceptance check:** the grep prints 0 for every file; `curl --replay rec -L http://127.0.0.1:45020/redir -w "%{http_code} %{num_redirects}"` still ends `200 1`.

## Acceptance criteria

- [ ] the grep prints 0 for every file; `curl --replay rec -L http://127.0.0.1:45020/redir -w "%{http_code} %{num_redirects}"` still ends `200 1`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-090 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: a452a8e7 69116afe 3d836d5e.
