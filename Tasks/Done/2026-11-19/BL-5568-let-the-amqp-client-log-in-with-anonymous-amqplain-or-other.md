---
id: BL-5568
title: Let the amqp client log in with ANONYMOUS, AMQPLAIN or other offered SASL mechanisms, not only PLAIN
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientFlow.cs, Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientMessages.cs, Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0Mechanism.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5568 — Let the amqp client log in with ANONYMOUS, AMQPLAIN or other offered SASL mechanisms, not only PLAIN

## Goal

The `amqp://` (AMQP 0-9-1) client picks a SASL mechanism the server offers in `Connection.Start`, so it can log in to a server that offers ANONYMOUS, AMQPLAIN, EXTERNAL or CRAM-MD5 but not PLAIN.

## Context

From bug report `2026-11-19T0930-Curl-bug-report.md`, finding 5 (Low). Build tested: commit `82913be53d`, Windows 11; server Surl at `71807f05f`. The *Surly* recipe `amqp-send-and-receive` has a case showing it, with the exact command in its `review.md`.

The client logs in only with PLAIN, so against a server that does not offer PLAIN it exits 67.

**Spec:** AMQP 0-9-1 `Connection.Start` lists the server's `mechanisms`; the client answers `Connection.StartOk` with one of them and its response. RabbitMQ's mechanisms are PLAIN, AMQPLAIN, EXTERNAL (with a TLS client certificate) and ANONYMOUS, plus RABBIT-CR-DEMO; CRAM-MD5 is offered by some servers through plugins. Choose the client's preference order (for example: EXTERNAL when `--cert` is given, then PLAIN or AMQPLAIN when `-u` is given, then ANONYMOUS without `-u`), and add CRAM-MD5 if a server the cookbook uses offers it. Record the order in an ADR (decided by Claude under Stewart's delegation). Reuse `Curl.Authentication.UnitLibrary`'s SASL pieces where they fit, without referencing an original protocol library.

**Where to start:** `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientFlow.cs` and `Amqp0ClientMessages.cs`.

## Acceptance criteria

- [x] An ADR records the mechanisms supported and the preference order.
- [x] Against a server offering only ANONYMOUS, the client without `-u` connects and exits 0; against one offering only AMQPLAIN, the client with `-u` logs in and exits 0.
- [x] Against a server offering only mechanisms the client cannot do, it exits 67 as now, without sending credentials.
- [x] Focused unit tests in the AMQP 0-9-1 library's `.UnitTests` pin each mechanism's `StartOk` bytes and the choice between offered mechanisms.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- ADR-1312. New file Amqp0Mechanism.cs holds the order (PLAIN, ANONYMOUS without -u, AMQPLAIN); EXTERNAL and CRAM-MD5 left out (no client cert reaches the handler; no challenge round). Mechanism choice/bytes pinned in Amqp0ClientFlowTests, no-usable-mechanism exit 67 in the handler test; quality gate passes for the library. Committed with the task.

## Log

- 2026-11-19: Created from bug report 2026-11-19T0930-Curl-bug-report.md, finding 5.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 153b29b7 da459af3 76972af5 5a1767f3 3d836d5e.
- 2026-11-19: Doing -> Done. amqp client picks PLAIN, ANONYMOUS or AMQPLAIN (ADR-1312)
