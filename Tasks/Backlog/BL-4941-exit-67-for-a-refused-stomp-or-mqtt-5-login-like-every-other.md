---
id: BL-4941
title: Exit 67 for a refused STOMP or MQTT 5 login like every other scheme
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary, Curl.Extended.Protocol.Mqtt5.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4941 — Exit 67 for a refused STOMP or MQTT 5 login like every other scheme

## Goal

A refused login on stomp (ERROR at CONNECT) and mqtt5 (CONNACK 0x86 / 0x87) exits 67, as redis, nats, amqp, amqp1, kafka, xmpp and curl itself do.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-019 (Low, confirmed: reproduced at least twice).

- Repro: STOMP mock on 18610 answering CONNECT with `ERROR\nmessage:Bad CONNECT\c login refused\n\n\0`; `curl -u bad:x stomp://127.0.0.1:18610/queue/a`. MQTT 5 mock on 18623 answering CONNECT with `20 03 00 86 00`; `curl -u a:b mqtt5://127.0.0.1:18623/t`.
- Expected: Exit 67.
- Actual: `curl: (8) STOMP server sent ERROR: Bad CONNECT: login refused`; `curl: (8) MQTT 5 CONNACK reason code 0x86 BadUserNameOrPassword`.
- Where to start: STOMP's ERROR-at-CONNECT mapping and MQTT 5's CONNACK mapping. Decided by Claude under Stewart's delegation (2026-11-11): consistency with curl's login-denied code wins; supersede ADR-1059 and ADR-1049 with an ADR.

## Acceptance criteria

- [ ] Both repros exit 67.
- [ ] An ADR supersedes ADR-1059 and ADR-1049, and both conformance documents state exit 67.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
