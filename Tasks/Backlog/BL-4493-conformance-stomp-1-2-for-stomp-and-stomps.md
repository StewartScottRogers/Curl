---
id: BL-4493
title: Conformance: STOMP 1.2 for stomp:// and stomps://
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4462]
touches: [Curl.Conformance.Extended.UnitLibrary/Stomp, Curl.Conformance.Extended.UnitTests/Stomp, Documentation/Conformance/Extended/Stomp.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4493 — Conformance: STOMP 1.2 for stomp:// and stomps://

## Goal

Curl's `stomp://` and `stomps://` client is measured against STOMP 1.2 through the real curl command line, every
client requirement is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: STOMP Protocol Specification, Version 1.2 (stomp.github.io/stomp-specification-1.2.html): frame
format and EOLs, value encoding (escapes), repeated headers (first wins), size limits, `content-length` and
`content-type`, connecting (CONNECT/STOMP, `accept-version`, `host`, `login`/`passcode`, `heart-beat`), protocol
negotiation, heart-beating, client frames (SEND, SUBSCRIBE with `id` and `ack`, UNSUBSCRIBE, ACK/NACK, DISCONNECT with
`receipt`), server frames (CONNECTED, MESSAGE with `subscription` and `message-id`, RECEIPT, ERROR). The spec uses
MUST, SHOULD and MAY; every one that binds a client gets an ID.

Library: `Curl.Extended.Protocol.Stomp.UnitLibrary` (`CLAUDE.md`: `Client/StompClientFlow`, `Wire/StompFrameEncoding`,
`StompFrameReader`; ADR-1059). Ports 61613 and 61614 (TLS); the URL path is the destination; `-u` gives login and
passcode. With a body it sends and disconnects with a receipt; without one it subscribes and writes each MESSAGE
body until the server closes.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Stomp:STOMP12-<section>-<n>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference broker implements the protocol from the specification
itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`.
This task touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is
fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Stomp conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Stomp.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of STOMP 1.2
      that binds a client a `Stomp:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Stomp/` run curl in process for `stomp://` and `stomps://` and
      assert frame bytes, stdout, stderr and exit code for at least: CONNECT headers (`accept-version`, `host`,
      credentials, `heart-beat`), a server choosing another version, an ERROR at connect, SEND with
      `content-length` and a body holding NUL, header escapes both ways (and none in CONNECT/CONNECTED), repeated
      headers, CR LF frames, heart-beat EOLs between frames, MESSAGE frames with and without `content-length`, a
      DISCONNECT receipt and a missing one, and a frame split across reads.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Stomp conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Stomp/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
