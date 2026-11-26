---
id: BL-6033
title: Fix fitness finding EXT-097: TLS extended protocols report zero %{time_total}, empty %{remote_ip} and %{num_connects} 0 when -m ends the TLS handshake; a receive ended by -m stamps pretransfer and starttransfer at the end (PARTLY FIXED)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6033 — Fix fitness finding EXT-097: TLS extended protocols report zero %{time_total}, empty %{remote_ip} and %{num_connects} 0 when -m ends the TLS handshake; a receive ended by -m stamps pretransfer and starttransfer at the end (PARTLY FIXED)

## Goal

EXT-097 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: TLS extended protocols report zero %{time_total}, empty %{remote_ip} and %{num_connects} 0 when -m ends the TLS handshake; a receive ended by -m stamps pretransfer and starttransfer at the end (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

This is an extension (Part B: extended protocols and extension options): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (2 runs) · **Area:** `-w` timing for amqps, rediss, stomps, mqtt5s, kafkas, amqp1s, xmpps, grpcs, mcps, socketios; receives on nats, mqtt5, stomp. Windows build.
- **What is fixed:** coap, coaps, dns, nats, grpc, socketio, mcp and signalr timeouts now report the elapsed time, `nc=1` and the server address; a receive no longer reports `starttransfer` 0.
- **Server:** a listener on `127.0.0.1:28950` that accepts TCP and UDP and never answers.
- **Repro:** `curl -sS -m 2 -o out.bin -w "[%{exitcode} c=%{time_connect} t=%{time_total} ip=%{remote_ip}:%{remote_port} nc=%{num_connects}]\n" amqps://127.0.0.1:28950/%2F/q` (and `rediss://…/PING`, `stomps://…/q`, `mqtt5s://…/t`, `kafkas://…/t`, `amqp1s://…/q`, `xmpps://…/a@b`, `grpcs://…/a.B/C`, `mcps://…/`, `socketios://…/`). Receive case: with `nats:2.15.3-alpine --auth s3cret` on 28422, `(sleep 2.5; curl -sS -u s3cret: -d hello nats://127.0.0.1:28422/wo9) & curl -sS -m 5 -u s3cret: -o o1.txt -w "[pt=%{time_pretransfer} st=%{time_starttransfer} t=%{time_total}]\n" nats://127.0.0.1:28422/wo9`.
- **Expected:** as the port's plain-TCP extended protocols and its https: `[28 … t=2.0xx ip=127.0.0.1:28950 nc=1]`. For the receive: `pt` is the setup time (under 1 s) and `st` is when the message arrived (about 2.5–4 s).
- **Actual (.NET):** `curl: (28) Connection timed out after 20xx milliseconds`, `[28 c=0.000000 t=0.000000 ip=:-1 nc=0]`. Receive: `[pt=5.013 st=5.013 t=5.013]`.
- **Likely location:** `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` (`StampTimingsAndUploadSize`, `TransferTimings`): the TLS-connect timeout path does not stamp timings, and the first-byte time is stamped at the end of the transfer.
- **Acceptance check:** the amqps repro prints `t` ≥ 2, `ip=127.0.0.1:28950`, `nc=1`; the NATS receive prints `pt` < 1 and 2 < `st` < `t`.

## Acceptance criteria

- [ ] the amqps repro prints `t` ≥ 2, `ip=127.0.0.1:28950`, `nc=1`; the NATS receive prints `pt` < 1 and 2 < `st` < `t`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
