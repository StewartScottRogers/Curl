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
completed: 2026-11-02
---
# BL-4608 — Conformance: WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) matrix and cases

## Goal

Curl's `webtransport://` client is measured through the real curl command line against WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16) and the RFCs it rests on. Every client requirement is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

This is what is left of BL-4494 after its split: BL-4606 gives the HTTP/3 stand-in QUIC datagrams, and BL-4607 builds the WebTransport reference server in `Curl.Conformance.Extended.UnitLibrary/WebTransport/Server/`. BL-4494's Context (in `Tasks/Deferred`) gives the specifications, the library under test (`Curl.Extended.Protocol.WebTransport.UnitLibrary`, ADR-1089) and the rules: the `[SpecRequirement]` IDs (`WebTransport:WTH3-<section>-<n>`, `WebTransport:RFC9297-<section>-<n>` and the like), and the matrix format and `KnownFailures.txt` rule in `Documentation/Conformance/Extended/README.md`. Put one file per case group under `Curl.Conformance.Extended.UnitTests/WebTransport/Cases/`.

A fault in the reference server is fixed here. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix WebTransport conformance: <requirement>`, with pipeline feature, touching the library, its `.UnitTests` and `KnownFailures.txt`. That task's criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), and the `KnownFailures.txt` line is removed.

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/WebTransport.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of draft-ietf-webtrans-http3-16 (and of RFC 9220, 9297 and 9221 as the session relies on them) that binds a client a `WebTransport:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/WebTransport/Cases/` run curl in process and assert SETTINGS, the CONNECT field section, stream and datagram bytes, stdout, stderr and exit code for at least: a server lacking the WebTransport or datagram settings, a non-2xx CONNECT response, a bidirectional exchange, a unidirectional one, a datagram one, the session id in stream headers and datagrams, `WT_CLOSE_SESSION` from the server with a code and a message, and the client's own close.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix WebTransport conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] The code under `Curl.Conformance.Extended.UnitLibrary/WebTransport/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

- Reference server fault fixed here: it advertised `SETTINGS_WT_MAX_SESSIONS` (`0x14e9cd29`, earlier drafts); draft 16 offers WebTransport with `SETTINGS_WT_ENABLED` (`0x2c7cf000`) = 1, which the client checks. `WebTransportServerScript.MaxSessions` became `WebTransportEnabled` (ADR-1213).
- Matrix: 19 rows in `Documentation/Conformance/Extended/WebTransport.md`, 17 mapped to cases and 2 `n/a` (transport parameters the reference server does not record). Cases, one file per group in `WebTransport/Cases/`: settings (client SETTINGS; server lacking WT_ENABLED, ENABLE_CONNECT_PROTOCOL or H3_DATAGRAM -> exit 8, no CONNECT), CONNECT (field section; 404 -> exit 22, no stream), exchange (bidirectional, unidirectional, datagram with session id and quarter stream id), close (the client's own WT_CLOSE_SESSION then FIN; the server's WT_CLOSE_SESSION with code 7 and a message).
- Measured: the settings and 404 cases pass. Every case whose session succeeds fails in the client: `WebTransportProtocolHandler.TransferAsync` calls `CloseAsync` on a `QuicConnection` that `Http3ClientSession`'s `await using` already disposed (ObjectDisposedException); listed with BL-4633. The server-close case times out because the client never reads the CONNECT stream; listed with BL-4634, which depends on BL-4633. Choice for the server-close outcome: exit 56 with `The server closed the WebTransport session: code <code>, <message>` (ADR-1213).
- The server-close case runs with `-m 3`, so the listed failure ends in 3 s instead of after the runner's default `-m 10`.
- Checks: `dotnet build Curl.slnx -warnaserror` clean; fast tests green on every test project (Curl.Conformance.Extended.UnitTests: 964 passed, the matrix and known-failures checks included); `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`: 0 failing members. No package added. The cases use only loopback names, with no paths and no OS text, so they do not depend on the platform; CI on Linux and macOS is left to the shift's CI watch.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 06fab208 e13a7e37 feec4e2a d8e3fb0e 28ebdf4f 17248609.
- 2026-11-02: Doing -> Done. WebTransport over HTTP/3 has a 19-row matrix and 12 in-process cases; client failures listed with BL-4633 and BL-4634
