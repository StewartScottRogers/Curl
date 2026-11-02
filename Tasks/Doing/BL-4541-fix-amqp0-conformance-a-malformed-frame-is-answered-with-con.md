---
id: BL-4541
title: Fix Amqp0 conformance: a malformed frame is answered with connection.close 501 (frame-error)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Client, Curl.Extended.Protocol.Amqp0.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4541 — Fix Amqp0 conformance: a malformed frame is answered with connection.close 501 (frame-error)

## Goal

When the amqp:// client reads a malformed frame (frame-end not %xCE, an unknown frame type, a heartbeat off channel 0),
it signals a connection exception: it sends connection.close with reply code 501 (frame-error) on channel 0 before it
closes the socket, and still exits 8 with its current message.

## Context

AMQP 0-9-1 (2008-11-13) section 4.2.3 and 2.3.7 (connection exceptions). Found by BL-4464's case
`Amqp0ConnectionTests.Frame_Malformed_IsAnsweredWithConnectionClose501` (requirement `Amqp0:AMQP091-4.2.3-4`):
today `Amqp0ClientFlow.RunAsync` turns `Amqp0WireFormatException` into exit 8 and closes without a word. Start in
`Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientFlow.cs` (the `catch (Amqp0WireFormatException)`): send
`Amqp0ConnectionClose(replyCode, text, 0, 0)` on channel 0, best effort (ignore a send failure), optionally wait briefly
for close-ok.

## Acceptance criteria

- [ ] `Amqp0ConnectionTests.Frame_Malformed_IsAnsweredWithConnectionClose501` passes without its KnownFailures.txt line.
- [ ] The `Amqp0:AMQP091-4.2.3-4 BL-4514` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] A focused unit test in `Curl.Extended.Protocol.Amqp0.UnitTests` pins the connection.close 501 (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 283f2cab 6b262408 a073a36d cb53e249.
