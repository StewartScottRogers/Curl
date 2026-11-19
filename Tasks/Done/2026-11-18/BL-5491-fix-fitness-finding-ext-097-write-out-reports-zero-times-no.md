---
id: BL-5491
title: Fix fitness finding EXT-097: Write-out reports zero times, no remote address and num_connects 0 when -m ends a messaging transfer that was connected and receiving
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5491 — Fix fitness finding EXT-097: Write-out reports zero times, no remote address and num_connects 0 when -m ends a messaging transfer that was connected and receiving

## Goal

EXT-097 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Write-out reports zero times, no remote address and num_connects 0 when -m ends a messaging transfer that was connected and receiving. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (nats, mqtt5, stomp twice each; also redis and kafka)
- **Protocol/area:** write-out for nats, mqtt5, stomp, redis, kafka subscribe/consume ended by a timeout
- **Repro:**
  1. `docker run -d -p 127.0.0.1:46222:4222 nats:2-alpine --auth s3cret`; `docker run -d -p 127.0.0.1:46883:1883 eclipse-mosquitto:2 sh -c 'printf "listener 1883\nallow_anonymous true\n" > /tmp/m.conf; exec mosquitto -c /tmp/m.conf'`
  2. `(sleep 1; curl -sS -u s3cret: -d hello nats://127.0.0.1:46222/wo9) & curl -sS -m 3 -u s3cret: -o NUL -w "[%{exitcode} c=%{time_connect} t=%{time_total} down=%{size_download} ip=%{remote_ip}:%{remote_port} nc=%{num_connects}]\n" nats://127.0.0.1:46222/wo9`
  3. The same with `mqtt5://127.0.0.1:46883/wo9` (publishing with `mqtt5://` and `-d hello`).
  4. Control: a TCP listener on 127.0.0.1:46999 that never answers; `curl -sS -m 3 -o NUL -w "<same format>" http://127.0.0.1:46999/` → `[28 c=0.002533 t=3.007497 down=0 ip=127.0.0.1:46999 nc=1]`.
- **Expected:** subscribing until `-m` is the normal way to stop a nats or mqtt5 subscription (`--extended-help`: nats subscribes "until the server closes or ?count=N"; mqtt5 "until the broker sends DISCONNECT"). The write-out gives the real connect time, a total of about 3 s, the remote address and num_connects 1, as the port does for HTTP.
- **Actual (PORT):** `curl: (28) Operation timed out after 3005 milliseconds with 6 bytes received`, `[28 c=0.000000 t=0.000000 down=6 ip=:-1 nc=0]`; mqtt5 and stomp the same shape.
- **Likely location:** `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` `StampTimingsAndUploadSize`: `if (result.Report is null && !result.IsSuccess) return result;` (BL-5421) drops the timings and end points even after `recorder.FirstConnectTimings` was recorded. Stamp a report whenever the recorder saw a connection.
- **Acceptance check:** step 2 prints `[28 c=0.00… t=3.0… down=6 ip=127.0.0.1:46222 nc=1]` with c > 0 and t ≥ 3.

## Acceptance criteria

- [x] step 2 prints `[28 c=0.00… t=3.0… down=6 ip=127.0.0.1:46222 nc=1]` with c > 0 and t ≥ 3.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `-m` cancels the handler, so no result carries the connected transfer's report; `TransferPerformer` replaced it with the watchdog failure. Fix: `EndPointReportingProtocolHandler` puts the stamped report (timings, end points, count) on the cancellation's `Data`; `TransferPerformer.WithReportOf` copies it onto the watchdog failure. Added `TransferPerformer.cs` to touches (no other Doing task holds it). Not run against live NATS (no docker); pinned by unit tests. Quality run: 0 failing members.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-097 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 49e32469 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. EXT-097 fixed: timed-out connected messaging transfers keep timings and end points
