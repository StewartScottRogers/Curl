---
id: BL-4465
title: Conformance: AMQP 0-9-1 channel, queue.declare, basic.publish and basic.get for amqp:// (Amqp0 part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4464]
touches: [Curl.Conformance.Extended.UnitLibrary/Amqp0/Messaging, Curl.Conformance.Extended.UnitTests/Amqp0/Messaging, Documentation/Conformance/Extended/Amqp0-Messaging.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4465 — Conformance: AMQP 0-9-1 channel, queue.declare, basic.publish and basic.get for amqp:// (Amqp0 part 2 of 2)

## Goal

Curl's `amqp://` publish and get are measured against the AMQP 0-9-1 channel, queue and basic classes and content
framing through the real curl command line, every client requirement of those parts is in a matrix mapped to a test,
and every failure is fixed or listed with its fix task.

## Context

Specification: AMQP 0-9-1 (2008-11-13) with RabbitMQ's errata. Scope of this part: the channel class (open/open-ok,
close/close-ok, flow), `queue.declare`, `basic.publish`, `basic.get`/`get-ok`/`get-empty`, `basic.return`, the content
header (4.2.6.1, basic properties) and body frames split at frame-max (4.2.6.2), and channel exceptions with their
reply codes. Builds on the reference broker BL-4464 wrote in `Curl.Conformance.Extended.UnitLibrary/Amqp0/`; add
behaviour in `Amqp0/Messaging/` through the extension point it left, without editing its files.

Library: `Curl.Extended.Protocol.Amqp0.UnitLibrary` (`CLAUDE.md`; `Methods/`, `Wire/Amqp0Content*`,
`Client/Amqp0ClientFlow`; ADR-1056, ADR-1060).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Amqp0:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference server implements the wire format from the specification itself and
references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Amqp0 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Amqp0-Messaging.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      this scope that binds a client an `Amqp0:` ID and maps it to a test or to `n/a: <reason>`; the harness's matrix
      check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/Amqp0/Messaging/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: publish of an empty body, of a body under and over frame-max, a
      get that returns a message, `get-empty`, a mandatory publish returned, a channel closed by the broker with
      each reply code the client maps, and a body frame on the wrong channel.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Amqp0 conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/Amqp0/Messaging/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Added `Amqp0/Messaging/`: `Amqp0BrokerContent` (writes and reads content header and body frames), `Amqp0BrokerContentHeader`, and `Amqp0MessagingMethods`, which adds answers through `Amqp0MethodTable.With` (get-ok with a message, basic.return, channel.close on queue.declare, channel.flow before declare-ok). BL-4464's files are unchanged.
- Cases in `Amqp0/Messaging/Amqp0MessagingTests.cs`; matrix `Documentation/Conformance/Extended/Amqp0-Messaging.md` has 22 rows, 3 of them n/a (flow sent by the client, basic.ack, consume).
- Choice: the client publishes with mandatory and immediate clear (RabbitMQ refuses immediate), so the "mandatory publish returned" case has the broker send basic.return unasked. The client must still read the return and its content and close cleanly. Today the client fails with 503 because the reader does not know method 60.50. Listed as known failure BL-4563.
- channel.flow (20.20) is not in the client's method reader either: the client fails with 503 and never sends flow-ok. Listed as known failure BL-4564.
- Channel exceptions: the client sends close-ok and nothing else on the channel; 403 exits 67; 404, 405 and 406 exit 8 with "AMQP server sent channel.close: reply code N, text". A body frame on another channel closes the connection with 505 and exits 8.
- Checks: build -warnaserror clean; fast tests green (113 projects, 0 failed); Measure-CodeQuality -Library Curl.Conformance.Extended.UnitLibrary: 0 failing members. No package added. The tests use no paths, files or OS text, so they do not depend on the platform. CI on Linux and macOS is met by that platform-neutral change.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: e9049952 ea5999b7 0854df25 d8e3fb0e 62863a38 ef6c9026.
- 2026-11-02: Doing -> Done. Amqp0 messaging conformance: 22-row matrix, publish/get/content/channel-exception cases; basic.return and channel.flow listed as BL-4563, BL-4564
