---
id: BL-4608
title: Conformance: WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) matrix and cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4606, BL-4607]
touches: [Curl.Conformance.Extended.UnitLibrary/WebTransport, Curl.Conformance.Extended.UnitTests/WebTransport/Cases, Documentation/Conformance/Extended/WebTransport.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4608 — Conformance: WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) matrix and cases

## Goal

Curl's `webtransport://` client is measured through the real curl command line against WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) and the RFCs it rests on. Every client requirement is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

This is what is left of BL-4494 after its split: BL-4606 gives the HTTP/3 stand-in QUIC datagrams, and BL-4607 builds the WebTransport reference server in `Curl.Conformance.Extended.UnitLibrary/WebTransport/Server/`. BL-4494's Context (in `Tasks/Deferred`) gives the specifications, the library under test (`Curl.Extended.Protocol.WebTransport.UnitLibrary`, ADR-1089) and the rules: the `[SpecRequirement]` IDs (`WebTransport:WTH3-<section>-<n>`, `WebTransport:RFC9297-<section>-<n>` and the like), and the matrix format and `KnownFailures.txt` rule in `Documentation/Conformance/Extended/README.md`. Put one file per case group under `Curl.Conformance.Extended.UnitTests/WebTransport/Cases/`.

A fault in the reference server is fixed here. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix WebTransport conformance: <requirement>`, with pipeline feature, touching the library, its `.UnitTests` and `KnownFailures.txt`. That task's criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), and the `KnownFailures.txt` line is removed.

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/WebTransport.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of draft-ietf-webtrans-http3-16 (and of RFC 9220, 9297 and 9221 as the session relies on them) that binds a client a `WebTransport:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/WebTransport/Cases/` run curl in process and assert SETTINGS, the CONNECT field section, stream and datagram bytes, stdout, stderr and exit code for at least: a server lacking the WebTransport or datagram settings, a non-2xx CONNECT response, a bidirectional exchange, a unidirectional one, a datagram one, the session id in stream headers and datagrams, `WT_CLOSE_SESSION` from the server with a code and a message, and the client's own close.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix WebTransport conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [ ] The code under `Curl.Conformance.Extended.UnitLibrary/WebTransport/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 06fab208 e13a7e37 feec4e2a d8e3fb0e 28ebdf4f 17248609.
