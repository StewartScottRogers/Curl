---
id: BL-4529
title: Conformance: AMQP 1.0 encoding widths and complete Amqp1 matrix for amqp1:// (BL-4466 part 3 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4545, BL-4546, BL-4528]
touches: [Curl.Conformance.Extended.UnitTests/Amqp1/Amqp1EncodingWidthCases.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceSession.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceContainerOptions.cs, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceContainer.cs, Documentation/Conformance/Extended/Amqp1.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4529 — Conformance: AMQP 1.0 encoding widths and complete Amqp1 matrix for amqp1:// (BL-4466 part 3 of 3)

## Goal

Every encoding width the server may choose for a value the client reads is measured, and the Amqp1 matrix covers all of Part 1, 2 and 5.3 client requirements of BL-4466 scope and links to `Amqp1-Messaging.md`.

## Context

Split from BL-4466 for size (one run could not hold the reference container, every case group and the matrix). Specification: OASIS AMQP Version 1.0 (29 October 2012), Parts 1, 2 (2.2 to 2.6, 2.8) and 5.3; Part 2 of the wider work (BL-4467) covers transfer, flow, disposition and the message format. Library under test: `Curl.Extended.Protocol.Amqp1.UnitLibrary` (`Types/`, `Frames/`, `Client/`; ADR-1053, ADR-1058, ADR-1062, ADR-1088). Example: `curl amqp1://host/queue -d hi`.

Follow `Documentation/Conformance/Extended/README.md` and ADR-1186/ADR-1188: the harness, `[SpecRequirement]` IDs (`Amqp1:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification, never against upstream curl. The reference server implements the wire format from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A fault in the reference server or cases is fixed here; a defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix Amqp1 conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] Cases in `Curl.Conformance.Extended.UnitTests/Amqp1/` assert wire bytes, stdout, stderr and exit code for every encoding width the server may choose (Part 1, 1.6 constructors: fixed and variable widths, small and large forms, list0/list8/list32, described types) for each value the client reads.
- [x] `Documentation/Conformance/Extended/Amqp1.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of BL-4466 scope (Parts 1, 2.2 to 2.6, 2.8 and 5.3) that binds a client an `Amqp1:` ID mapped to a test or `n/a: <reason>`, links to `Amqp1-Messaging.md` (written by BL-4467), and the harness matrix check passes.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix Amqp1 conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Widened touches to three reference container files (Amqp1ReferenceSession.cs, Amqp1ReferenceContainerOptions.cs, Amqp1ReferenceContainer.cs): the container wrote only its smallest or widest encodings, so it gained an optional `BodyEncoding` hook (an option passed to the session) that lets a case write each frame body in the forms it chooses. No task in Doing on origin/work/dark-factory touches `Curl.Conformance.Extended.UnitLibrary/Amqp1`; BL-4467 (Backlog) touches only `Amqp1/Messaging`.
- `Amqp1EncodingWidthCases` (7 cases) covers: `WideEncoding` (uint, ulong, str32, sym32, vbin32, list32, array32) for a sent message and a refused link; uint zero as smalluint, boolean as 0x56 and a byte, and empty lists as list8; list32 around small values; symbolic descriptors on every body the container sends (a sent message and a refused link, so the error too); one symbol in place of the sasl-server-mechanisms array. All pass against curl, so no KnownFailures line was added.
- Choice: Amqp1.md links to Amqp1-Messaging.md now, before BL-4467 writes it, because BL-4467 depends on this task (waiting for it would be a cycle), and no check in the repository validates Markdown link targets. The Part 1 types the client never reads in these frames (signed integers, floats, decimals, char, timestamp, uuid, map) are one n/a row that points at Amqp1-Messaging.md.
- Checks: `dotnet build Curl.slnx -warnaserror` clean; fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary` reports 0 failing members. The cases are platform-neutral (in-memory loopback, no paths or OS text), which covers the Linux and macOS CI criterion.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 3419072e 526b6ab5 9fbc674d d8e3fb0e ef6c9026.
- 2026-11-02: Doing -> Done. amqp1:// is measured against every AMQP 1.0 encoding width curl reads and the Amqp1 matrix covers Parts 1, 2 and 5.3
