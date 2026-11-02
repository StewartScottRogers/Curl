---
id: BL-4466
title: Conformance: AMQP 1.0 types, transport, connection, session, link and SASL for amqp1:// (Amqp1 part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4501, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Amqp1, Curl.Conformance.Extended.UnitTests/Amqp1, Documentation/Conformance/Extended/Amqp1.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4466 — Conformance: AMQP 1.0 types, transport, connection, session, link and SASL for amqp1:// (Amqp1 part 1 of 2)

## Goal

Curl's `amqp1://` and `amqp1s://` client is measured against OASIS AMQP 1.0's type system, transport and SASL layer
through the real curl command line, every client requirement of those parts is in a matrix mapped to a test, and
every failure is fixed or listed with its fix task.

## Context

Specification: OASIS AMQP Version 1.0 (29 October 2012). Scope of this part: Part 1 (types and encodings, section
1.6 constructors), Part 2 (2.2 version negotiation and protocol header, 2.3 framing, 2.4 connections: open, close,
idle timeout, max-frame-size; 2.5 sessions: begin, end; 2.6 links: attach, detach; 2.8 errors) and Part 5 (5.3 SASL:
mechanisms, init with PLAIN and ANONYMOUS, outcome). Part 2 of this work (BL-4467) covers transfer, flow,
disposition and the message format.

Library: `Curl.Extended.Protocol.Amqp1.UnitLibrary` (`CLAUDE.md`: `Types/`, `Frames/`, `Client/`; ADR-1053,
ADR-1058, ADR-1062, ADR-1088). Example: `curl amqp1://host/queue -d hi`.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Amqp1:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference server implements the wire format from the specification itself and
references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Amqp1 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Amqp1.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of this scope
      that binds a client an `Amqp1:` ID and maps it to a test or to `n/a: <reason>`, links to `Amqp1-Messaging.md`
      (written by BL-4467), and the harness's matrix check passes.
- [ ] A reference AMQP 1.0 container in `Curl.Conformance.Extended.UnitLibrary/Amqp1/` lets BL-4467 add transfer
      behaviour in `Amqp1/Messaging/` without editing this task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Amqp1/` run curl in process for `amqp1://` and `amqp1s://` and
      assert wire bytes, stdout, stderr and exit code for at least: both protocol headers and a server answering
      with another one, SASL PLAIN from `-u`, ANONYMOUS without it, a SASL outcome other than ok, max-frame-size
      negotiation, idle-timeout empty frames, open/begin/attach/detach/end/close order, a close carrying an error,
      and every encoding width the server may choose for a value the client reads.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Amqp1 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Amqp1/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
