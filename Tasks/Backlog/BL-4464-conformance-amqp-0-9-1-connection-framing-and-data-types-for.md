---
id: BL-4464
title: Conformance: AMQP 0-9-1 connection, framing and data types for amqp:// (Amqp0 part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4462]
touches: [Curl.Conformance.Extended.UnitLibrary/Amqp0, Curl.Conformance.Extended.UnitTests/Amqp0, Documentation/Conformance/Extended/Amqp0.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4464 — Conformance: AMQP 0-9-1 connection, framing and data types for amqp:// (Amqp0 part 1 of 2)

## Goal

Curl's `amqp://` and `amqps://` client is measured against the AMQP 0-9-1 specification's protocol header, framing,
data types, field tables and connection class through the real curl command line, every client requirement of those
parts is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: AMQP 0-9-1 (2008-11-13) with RabbitMQ's errata and `amqp0-9-1.extended.xml` class and method ids.
Scope of this part: sections 2.2-2.3 (connection negotiation, heartbeat), 4.2.1-4.2.5 (protocol header, frames,
frame-end, frame-max, data types and field tables), the connection class (start/start-ok, secure, tune/tune-ok,
open/open-ok, close/close-ok) and the reply codes of a connection exception. Part 2 (BL-4465) covers channels,
`queue.declare`, `basic.publish`/`basic.get` and content frames.

Library: `Curl.Extended.Protocol.Amqp0.UnitLibrary` (read its `CLAUDE.md`: `Wire/`, `Methods/`, `Client/`;
ADR-1056 wire faults, ADR-1060 exit codes, ADR-1091 registration). Example: `curl amqp://host/vhost/queue -d hi`.

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

- [ ] `Documentation/Conformance/Extended/Amqp0.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of this scope
      that binds a client an `Amqp0:` ID and maps it to a test or to `n/a: <reason>`, links to `Amqp0-Messaging.md`
      (written by BL-4465), and the harness's matrix check passes.
- [ ] A reference AMQP 0-9-1 broker in `Curl.Conformance.Extended.UnitLibrary/Amqp0/` lets BL-4465 add channel and
      basic-class behaviour in `Amqp0/Messaging/` without editing this task's files (a partial class or an injected
      method-handler table).
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Amqp0/` run curl in process for `amqp://` and `amqps://` and
      assert wire bytes, stdout, stderr and exit code for at least: the protocol header and a broker that answers
      with another version, frame-end octet, frame-max and channel-max negotiation, heartbeat negotiation, PLAIN
      credentials from `-u` and the URL, an unknown or bad frame type, and a `connection.close` from the broker.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Amqp0 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Amqp0/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
