---
id: BL-4494
title: Conformance: WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) for the WebTransport extension
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/WebTransport, Curl.Conformance.Extended.UnitTests/WebTransport, Documentation/Conformance/Extended/WebTransport.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4494 — Conformance: WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) for the WebTransport extension

## Goal

Curl's `webtransport://` client is measured against WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) and the
RFCs it rests on through the real curl command line, every client requirement is in a matrix mapped to a test, and
every failure is fixed or listed with its fix task.

## Context

Specifications: draft-ietf-webtrans-http3-16 (SETTINGS the client sends and requires, the extended CONNECT with
`:protocol webtransport`, session ids, bidirectional and unidirectional stream headers, datagrams with the quarter
stream id, `WT_CLOSE_SESSION` capsule, session termination and error codes), RFC 9220 (extended CONNECT in HTTP/3),
RFC 9297 (HTTP datagrams and the capsule protocol), RFC 9221 (QUIC datagrams, `max_datagram_frame_size`), and
RFC 9114 where the session relies on it. The library pins draft 16; name it in the matrix.

Library: `Curl.Extended.Protocol.WebTransport.UnitLibrary` (no `CLAUDE.md`; `Session/WebTransportSession`,
`WebTransportQuicDialer`, `WebTransportRequest`, `Wire/WebTransportWire`, `WebTransportCloseSession`; ADR-1089). It
opens a session at `webtransport://host[:port]/path` (UDP 443), sends `-d` on a bidirectional stream and writes the
answer; `?wt=uni` and `?wt=datagram` use a unidirectional stream or a datagram. The reference server sits on
`Curl.Conformance.Http3Server.UnitLibrary` over the harness's UDP route.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`WebTransport:WTH3-<section>-<n>`, `WebTransport:RFC9297-<section>-<n>` and the like), the matrix format and the
`KnownFailures.txt` rule. Measure against the specification, never against upstream curl. The reference server
implements WebTransport from the specification itself and references no `Curl.Extended.Protocol.*` project. One file
per case group; nothing added to `Harness/`. This task touches only its own conformance files so protocols run in
parallel: a fault in its own server or cases is fixed here, but a defect in the extension library is listed in
`KnownFailures.txt` with a filed High task `Fix WebTransport conformance: <requirement>` (pipeline feature; touches
the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the
library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/WebTransport.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      draft-ietf-webtrans-http3-16 (and of RFC 9220, 9297 and 9221 as the session relies on them) that binds a client
      a `WebTransport:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/WebTransport/` run curl in process and assert SETTINGS, the CONNECT
      field section, stream and datagram bytes, stdout, stderr and exit code for at least: a server lacking the
      WebTransport or datagram settings, a non-2xx CONNECT response, a bidirectional exchange, a unidirectional one, a
      datagram one, the session id in stream headers and datagrams, `WT_CLOSE_SESSION` from the server with a code
      and a message, and the client's own close.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix WebTransport conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/WebTransport/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 06fab208 b8e0bf7a feec4e2a d8e3fb0e b686519a 17248609.
