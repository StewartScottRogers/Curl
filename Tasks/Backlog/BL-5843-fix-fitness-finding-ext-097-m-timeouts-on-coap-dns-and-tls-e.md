---
id: BL-5843
title: Fix fitness finding EXT-097: -m timeouts on coap, dns and TLS extension schemes report zero times; a receive ended by -m reports %{time_starttransfer} 0 (STILL OPEN, partly fixed)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5843 — Fix fitness finding EXT-097: -m timeouts on coap, dns and TLS extension schemes report zero times; a receive ended by -m reports %{time_starttransfer} 0 (STILL OPEN, partly fixed)

## Goal

EXT-097 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: -m timeouts on coap, dns and TLS extension schemes report zero times; a receive ended by -m reports %{time_starttransfer} 0 (STILL OPEN, partly fixed). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

This finding was filed before (fitness review 2026-11-18T0633) and is only partly fixed; the finding says which part still reproduces.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (each case 2 runs)
- **Protocol/area:** write-out for coap, dns, amqps, rediss, stomps, nats, mqtt5, stomp. Plain TCP messaging transfers ended by `-m` now report correct connect/total times.
- **Repro:**
  1. A black-hole server on 127.0.0.1:28950 that accepts TCP and UDP and never answers.
  2. `curl -sS -m 2 -o NUL -w "[%{exitcode} c=%{time_connect} t=%{time_total} ip=%{remote_ip}:%{remote_port} nc=%{num_connects}]\n" coap://127.0.0.1:28950/r`; the same with `dns://127.0.0.1:28950/x.com`, `amqps://127.0.0.1:28950/%2F/q`, `rediss://127.0.0.1:28950/PING`, `stomps://127.0.0.1:28950/q`.
  3. Receive case: `docker run -d -p 127.0.0.1:28222:4222 nats:2-alpine --auth s3cret`, then `(sleep 1; curl -sS -u s3cret: -d hello nats://127.0.0.1:28222/wo9) & curl -sS -m 3 -u s3cret: -o NUL -w "[pt=%{time_pretransfer} st=%{time_starttransfer} t=%{time_total} down=%{size_download}]\n" nats://127.0.0.1:28222/wo9`
- **Expected:** consistent with the port's own TCP schemes and tftp after a timeout (tftp: `[28 … t=2.008482 ip=127.0.0.1:28950 nc=1]`; https: t=2.01). A transfer that received bytes has 0 < st ≤ t.
- **Actual (.NET):** coap/dns: `curl: (28) Operation timed out after 2009 milliseconds…` then `[28 c=0.000000 t=0.000000 ip=127.0.0.1:28950 nc=1]`. amqps/rediss/stomps: `curl: (28) Connection timed out after 2007 milliseconds` then `[28 c=0.000000 t=0.000000 ip=:-1 nc=0]`. nats: `[pt=3.001193 st=0.000000 t=3.001193 down=6]` (mqtt5 and stomp the same shape).
- **Likely location:** `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` (`StampTimingsAndUploadSize` / `TransferTimings`): the UDP and TLS-handshake timeout paths do not stamp `time_total`, and `time_starttransfer` is not stamped when a receive is cancelled.
- **Acceptance check:** the coap command prints `t` ≥ 2 with `ip=127.0.0.1:28950 nc=1`; the amqps command prints `t` ≥ 2; the nats command prints 0 < st ≤ t.

## Acceptance criteria

- [ ] the coap command prints `t` ≥ 2 with `ip=127.0.0.1:28950 nc=1`; the amqps command prints `t` ≥ 2; the nats command prints 0 < st ≤ t.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
