---
id: BL-5475
title: Fix fitness finding STD-154: MQTT write-out: a subscribe that ends in a timeout reports every %{time_*} as 0; a publish reports pretransfer/posttransfer 0 and %{size_upload} = payload length
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5475 — Fix fitness finding STD-154: MQTT write-out: a subscribe that ends in a timeout reports every %{time_*} as 0; a publish reports pretransfer/posttransfer 0 and %{size_upload} = payload length

## Goal

STD-154 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: MQTT write-out: a subscribe that ends in a timeout reports every %{time_*} as 0; a publish reports pretransfer/posttransfer 0 and %{size_upload} = payload length. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** mqtt:// write-out, Windows
- **Repro:** `docker run -d --name t-mq -p 127.0.0.1:44883:1883 eclipse-mosquitto:2 mosquitto -c /mosquitto-no-auth.conf`; `docker exec t-mq mosquitto_pub -t a5/r -m retainedmsg -r`.
  - `curl -sS -m 2 -o NUL -w "sz=%{size_download} c=%{time_connect} pre=%{time_pretransfer} st=%{time_starttransfer} tot=%{time_total} sp=%{speed_download}\n" mqtt://127.0.0.1:44883/a5/r`
  - `curl -sS -m 5 -d x -w "up=%{size_upload} c=%{time_connect} pre=%{time_pretransfer} post=%{time_posttransfer} tot=%{time_total}\n" mqtt://127.0.0.1:44883/a5/t`
- **Expected (REF):** subscribe: `curl: (28) …` then `sz=17 c=0.001017 pre=2.008849 st=0.046107 tot=2.008852 sp=8`, exit 28. Publish: `up=0 c=0.001141 pre=0.002439 post=0.002440 tot=0.002489`, exit 0.
- **Actual (PORT):** subscribe: `sz=17 c=0.000000 pre=0.000000 st=0.000000 tot=0.000000 sp=0`, exit 28. Publish: `up=1 c=0.002540 pre=0.000000 post=0.000000 tot=0.004116`. Payloads and exit codes match.
- **Likely location:** `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttProtocolHandler.cs` / `MqttSession.cs` — no `TransferTimings` is built; the timeout path loses the connect timings; the publish payload is counted as upload.
- **Acceptance check:** the subscribe prints non-zero `c`, `st` and `tot` (≈ 2) and non-zero speed; the publish prints `up=0` and non-zero `pre`/`post`.

## Acceptance criteria

- [ ] the subscribe prints non-zero `c`, `st` and `tot` (≈ 2) and non-zero speed; the publish prints `up=0` and non-zero `pre`/`post`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-154 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 2eabf6aa 69116afe 3d836d5e.
