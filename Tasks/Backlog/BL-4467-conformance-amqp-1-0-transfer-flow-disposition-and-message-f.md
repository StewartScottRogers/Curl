---
id: BL-4467
title: Conformance: AMQP 1.0 transfer, flow, disposition and message format for amqp1:// (Amqp1 part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4462, BL-4466]
touches: [Curl.Conformance.Extended.UnitLibrary/Amqp1/Messaging, Curl.Conformance.Extended.UnitTests/Amqp1/Messaging, Documentation/Conformance/Extended/Amqp1-Messaging.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4467 — Conformance: AMQP 1.0 transfer, flow, disposition and message format for amqp1:// (Amqp1 part 2 of 2)

## Goal

Curl's `amqp1://` send and receive are measured against AMQP 1.0's transfer, flow control, settlement and message
format through the real curl command line, every client requirement of those parts is in a matrix mapped to a test,
and every failure is fixed or listed with its fix task.

## Context

Specification: OASIS AMQP Version 1.0 (29 October 2012). Scope of this part: Part 2 sections 2.6.7-2.6.13 and 2.7
(flow control, link credit, transfer, multi-frame transfers, disposition, settlement modes), Part 3 (3.2 message
format: header, properties, application-properties, data, amqp-value, footer; 3.4 delivery states: accepted,
rejected, released, modified; 3.5 source and target). Builds on the reference container BL-4466 wrote in
`Curl.Conformance.Extended.UnitLibrary/Amqp1/`; add behaviour in `Amqp1/Messaging/` through its extension point.

Library: `Curl.Extended.Protocol.Amqp1.UnitLibrary` (`Frames/Amqp1MessageCodec`, `Client/Amqp1ClientFlow`;
ADR-1058, ADR-1062).

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

- [ ] `Documentation/Conformance/Extended/Amqp1-Messaging.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      this scope that binds a client an `Amqp1:` ID and maps it to a test or to `n/a: <reason>`; the harness's matrix
      check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Amqp1/Messaging/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: a send waiting for credit, a send settled by accepted, rejected,
      released and modified, a transfer split over several frames (`more`), a receive of `data`, `amqp-value` and
      several `data` sections, a receive with no message, and a delivery-id or handle the server gets wrong.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Amqp1 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Amqp1/Messaging/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
