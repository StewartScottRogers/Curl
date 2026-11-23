---
id: BL-5842
title: Fix fitness finding EXT-114: NATS and AMQP 0-9-1 error messages print raw control characters (CR/LF, ESC) from the URL or the server's text
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsSubject.cs, Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientMessages.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5842 — Fix fitness finding EXT-114: NATS and AMQP 0-9-1 error messages print raw control characters (CR/LF, ESC) from the URL or the server's text

## Goal

EXT-114 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: NATS and AMQP 0-9-1 error messages print raw control characters (CR/LF, ESC) from the URL or the server's text. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:

- **Severity:** Low (terminal escape injection into stderr)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** nats, amqp (same class as the fixed EXT-101 for Kafka)
- **Repro:**
  1. `curl -sS -m 3 -d x "nats://127.0.0.1:28222/a%0D%0Ab" 2>&1 | od -c` (refused before connecting; no server needed)
  2. rabbitmq:4-alpine on 127.0.0.1:28672 with `default_user=rguest`, `default_pass=rpass`: `curl -sS -m 3 -u rguest:rpass "amqp://127.0.0.1:28672/nosuchvhost%1B%5B31m/q" 2>&1 | od -c`
- **Expected:** control characters escaped, as Kafka now does (`a\x0D\x0Ab`).
- **Actual (.NET):** (1) `curl: (3) NATS subject "a\r\nb" holds whitespace or an empty token` with raw CR LF; (2) `curl: (8) AMQP server sent connection.close: reply code 530, NOT_ALLOWED - vhost nosuchvhost\033[31m…` with a raw ESC (the terminal turns red).
- **Likely location:** `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSubject.cs`; `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientMessages.cs` (connection.close reply text).
- **Acceptance check:** `od -c` of both commands shows `\x0D\x0A` and `\x1B` text instead of raw bytes.

## Acceptance criteria

- [ ] `od -c` of both commands shows `\x0D\x0A` and `\x1B` text instead of raw bytes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.