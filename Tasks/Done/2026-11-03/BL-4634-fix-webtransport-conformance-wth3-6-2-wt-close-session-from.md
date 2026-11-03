---
id: BL-4634
title: Fix WebTransport conformance: WTH3-6-2 WT_CLOSE_SESSION from the server ends the transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4633]
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary, Curl.Extended.Protocol.WebTransport.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4634 — Fix WebTransport conformance: WTH3-6-2 WT_CLOSE_SESSION from the server ends the transfer

## Goal

When the server sends `WT_CLOSE_SESSION` on the CONNECT stream, curl ends the transfer at once with exit 56 and `curl: (56) The server closed the WebTransport session: code <code>, <message>`, closes its side of the CONNECT stream with a FIN and sends no capsule of its own (draft-ietf-webtrans-http3-16 section 6).

## Context

Found by BL-4608's case `WebTransportCloseTests.ServerClose_WithCodeAndMessage_EndsTheTransferWithExit56AndTheClientSendsOnlyFin` (`WebTransport:WTH3-6-2`, listed in `KnownFailures.txt`). `WebTransportSession` never reads the CONNECT stream after the response, so a server close leaves the transfer waiting on its bidirectional stream until `-m` (exit 28 today). The exit code and text are the case's choice (BL-4608 Notes): 56 as for any session the peer ends early, the message carrying the code and the UTF-8 reason. Depends on BL-4633, which fixes the crash after a session and touches the same files.

## Acceptance criteria

- [x] `WebTransportCloseTests.ServerClose_WithCodeAndMessage_EndsTheTransferWithExit56AndTheClientSendsOnlyFin` passes.
- [x] A focused unit test in `Curl.Extended.Protocol.WebTransport.UnitTests` pins the fix (ADR-0482).
- [x] The `WebTransport:WTH3-6-2 BL-4624` line is removed from `KnownFailures.txt`; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and `Curl.Extended.Protocol.WebTransport.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- `WebTransportSession` now watches the CONNECT stream after the 2xx response (`WatchForServerCloseAsync`): it decodes capsules from the DATA bytes, skips other types and a `WT_CLOSE_SESSION` too short for its error code (RFC 9297 section 3.2 ignores unknown capsules; skipping the malformed one is the simplest choice), and on the server's close records it and cancels the exchange. The exchange's failure is then reported as exit 56 `The server closed the WebTransport session: code <code>, <message>` after a bare FIN on the CONNECT stream (`EndRequestAsync`). A CONNECT stream that ends without a close leaves the exchange running, as before. Exit code and text are BL-4608's case choice, so no new ADR.
- Focused tests: `WebTransportProtocolHandlerTests.ExecuteAsync_Section6_ServerClose_EndsTheTransferWithExit56AndTheClientSendsOnlyFin` (capsule split across DATA frames, after an unknown and a too-short capsule) and `ExecuteAsync_Section6_SessionStreamEndsWithoutClose_TheExchangeStillCompletes`.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.WebTransport.UnitLibrary reports 0 failing members; `dotnet build Curl.slnx -warnaserror` clean; fast tests green; Curl.Conformance.Extended.UnitTests 1308 passed with the WTH3-6-2 line removed. Change is platform-neutral (in-memory streams, no paths or OS text).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 4f61f264 bd7a9132 79f20796 e6dffd78.
- 2026-11-02: Doing -> Done. A server WT_CLOSE_SESSION now ends the WebTransport transfer with exit 56 and a bare FIN
