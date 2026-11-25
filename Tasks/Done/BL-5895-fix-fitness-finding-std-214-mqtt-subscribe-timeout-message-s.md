---
id: BL-5895
title: Fix fitness finding STD-214: MQTT subscribe timeout message shows the wrong "out of" total
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Mqtt.UnitLibrary/MqttSession.cs, Curl.Original.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs, Curl.Original.Protocol.Mqtt.UnitTests/MqttProtocolHandlerTimeoutByteCountTests.cs, Curl.Original.Protocol.Mqtt.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5895 — Fix fitness finding STD-214: MQTT subscribe timeout message shows the wrong "out of" total

## Goal

STD-214 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: MQTT subscribe timeout message shows the wrong "out of" total. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Probable · **Area:** MQTT, Windows build.
- **Setup:** Mosquitto 2 with retained messages of 19, 20, 313 and 70007 bytes under topics `r/…`.
- **Repro:** `curl -sS -m 3 'mqtt://127.0.0.1:36884/r/%23'`
- **Expected (REF):** `curl: (28) Operation timed out after … with 70007 out of 70007 bytes received`.
- **Actual (.NET):** `… with 70359 out of 70007 bytes received`; with more retained messages it printed `70385 out of 13`. Stdout, the `-o` file and `%{size_download}` are identical to REF.
- **Likely location:** the MQTT handler's progress/expected-size reporting (the "out of" value is taken from one message's remaining length while the received count spans all messages, including MQTT framing).
- **Acceptance check:** the repro's timeout message shows the same two numbers as REF.

## Acceptance criteria

- [x] the repro's timeout message shows the same two numbers as REF.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced and measured against curl 8.22.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Script`, `-m 1`:
  PUBLISH bodies of 20 then 13 bytes print `13 out of 13`; 20 then 5 of 13 print `5 out of 13`; 20 then the header
  `30 0d` alone prints `20 out of 20` (curl resets `req.bytecount` and `req.size` only when body bytes are read).
  Built Curl prints the same three pairs (the header-only case replayed with `-m 3`: at `-m 1` the debug build
  timed out before the first message arrived).
- Fix: `MqttSession.PublishBytesWritten` counts the latest PUBLISH body and the handler's timeout message uses it.
  `%{size_download}`, the output and the progress sink's running total are unchanged. No ADR: no design choice.
- Direct run rather than subagent stages: a two-line behaviour change. Added the new test file and the library's
  CLAUDE.md to `touches`; no other task in Doing named an MQTT path.
- Gates: `dotnet build` clean; fast tests green (the one failure of the full run was this task's new test, whose
  fake cancelled inside the handler's look-ahead read; fixed and the MQTT test project rerun, 183 passed,
  5 platform skips); `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Mqtt.UnitLibrary`: 0 failing members;
  `dotnet format --verify-no-changes` clean on the changed C# files. Tests are platform-neutral.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 4c475da1 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. MQTT -m timeout message counts the latest PUBLISH body, as curl 8.22.0 does
