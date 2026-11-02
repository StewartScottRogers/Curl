---
id: BL-4528
title: Conformance: AMQP 1.0 connection, session, link, frame size, idle timeout and errors for amqp1:// (BL-4466 part 2 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4545, BL-4546]
touches: [Curl.Conformance.Extended.UnitTests/Amqp1/Amqp1ConnectionCases.cs, Curl.Conformance.Extended.UnitTests/Amqp1/Amqp1SessionLinkCases.cs, Documentation/Conformance/Extended/Amqp1.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4528 — Conformance: AMQP 1.0 connection, session, link, frame size, idle timeout and errors for amqp1:// (BL-4466 part 2 of 3)

## Goal

The AMQP 1.0 connection, session and link requirements (2.3 framing, 2.4, 2.5, 2.6 attach/detach, 2.8 errors) are measured against the BL-4527 reference container and in the Amqp1 matrix.

## Context

Split from BL-4466 for size (one run could not hold the reference container, every case group and the matrix). Specification: OASIS AMQP Version 1.0 (29 October 2012), Parts 1, 2 (2.2 to 2.6, 2.8) and 5.3; Part 2 of the wider work (BL-4467) covers transfer, flow, disposition and the message format. Library under test: `Curl.Extended.Protocol.Amqp1.UnitLibrary` (`Types/`, `Frames/`, `Client/`; ADR-1053, ADR-1058, ADR-1062, ADR-1088). Example: `curl amqp1://host/queue -d hi`.

Follow `Documentation/Conformance/Extended/README.md` and ADR-1186/ADR-1188: the harness, `[SpecRequirement]` IDs (`Amqp1:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification, never against upstream curl. The reference server implements the wire format from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A fault in the reference server or cases is fixed here; a defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix Amqp1 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Amqp1.md` gives every client MUST, MUST NOT, SHOULD and SHOULD NOT of 2.3, 2.4, 2.5, 2.6 (attach, detach) and 2.8 an `Amqp1:` ID mapped to a test or `n/a: <reason>`; the harness matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Amqp1/` assert wire bytes, stdout, stderr and exit code for at least: max-frame-size negotiation, idle-timeout empty frames, open/begin/attach/detach/end/close order, and a close carrying an error.
- [ ] Any code this task adds under `Curl.Conformance.Extended.UnitLibrary/Amqp1/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix Amqp1 conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b448894b 45bfdece 208eae6e 9fbc674d d8e3fb0e ef6c9026.
