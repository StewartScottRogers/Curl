---
id: BL-5492
title: Fix fitness finding EXT-098: %{size_upload} reports the -d data as uploaded when the broker refused the login before anything was published
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5492 — Fix fitness finding EXT-098: %{size_upload} reports the -d data as uploaded when the broker refused the login before anything was published

## Goal

EXT-098 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: %{size_upload} reports the -d data as uploaded when the broker refused the login before anything was published. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (4 protocols, twice each)
- **Protocol/area:** write-out for redis, amqp, mqtt5, kafka (SASL)
- **Repro:** brokers: `redis:7-alpine --requirepass rpw` on 127.0.0.1:46379; `rabbitmq:4-alpine` with `default_user=rguest` on 46672; mosquitto with a password file (user `mu`) on 46884; kafka SASL_PLAINTEXT with PLAIN user `alice` on 46095.
  - `curl -sS -m 5 -u :WRONG -d PAYLOAD12 -o NUL -w "[%{exitcode} up=%{size_upload}]\n" redis://127.0.0.1:46379/SET/k`
  - the same with `-u rguest:WRONG amqp://127.0.0.1:46672/%2F/q1`, `-u mu:WRONG mqtt5://127.0.0.1:46884/t`, `-u alice:WRONG kafka://127.0.0.1:46095/t`
- **Expected:** `size_upload` is the bytes actually uploaded; a login refused before SET / basic.publish / PUBLISH / Produce sent no body, so 0 — the port's own rule in `UploadSizeOf` ("one that never connected sent nothing") and its HTTP behaviour.
- **Actual (PORT):** `curl: (67) …` and `[67 up=9]` for all four.
- **Likely location:** `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` `UploadedBy` returns `context.PostData.Length` whenever there is `-d` data; have handlers report bytes actually published (`report.UploadSize`), or report 0 on failure.
- **Acceptance check:** the redis command prints `[67 up=0]`; with `-u :rpw` it prints `[0 up=9]`.

## Acceptance criteria

- [x] the redis command prints `[67 up=0]`; with `-u :rpw` it prints `[0 up=9]`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Decided: a failed non-HTTP transfer reports 0 uploaded of the -d data (a broker refusing the login sent no publish); HTTP keeps -d length. Fixed in EndPointReportingProtocolHandler.UploadedBy; unit tests added; quality run clean.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-098 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 1606b934 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Failed non-HTTP transfer reports 0 uploaded -d data
