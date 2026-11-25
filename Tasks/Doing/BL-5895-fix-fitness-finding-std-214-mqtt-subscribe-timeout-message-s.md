---
id: BL-5895
title: Fix fitness finding STD-214: MQTT subscribe timeout message shows the wrong "out of" total
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Mqtt.UnitLibrary/MqttSession.cs, Curl.Original.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs]
requirement: none
created: 2026-11-24
completed:
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

- [ ] the repro's timeout message shows the same two numbers as REF.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 4c475da1 69116afe 3d836d5e.
