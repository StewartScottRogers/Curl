---
id: BL-4624
title: Fix WebTransport conformance: WTH3-6-2 WT_CLOSE_SESSION from the server ends the transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4623]
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary, Curl.Extended.Protocol.WebTransport.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4624 — Fix WebTransport conformance: WTH3-6-2 WT_CLOSE_SESSION from the server ends the transfer

## Goal

When the server sends `WT_CLOSE_SESSION` on the CONNECT stream, curl ends the transfer at once with exit 56 and `curl: (56) The server closed the WebTransport session: code <code>, <message>`, closes its side of the CONNECT stream with a FIN and sends no capsule of its own (draft-ietf-webtrans-http3-16 section 6).

## Context

Found by BL-4608's case `WebTransportCloseTests.ServerClose_WithCodeAndMessage_EndsTheTransferWithExit56AndTheClientSendsOnlyFin` (`WebTransport:WTH3-6-2`, listed in `KnownFailures.txt`). `WebTransportSession` never reads the CONNECT stream after the response, so a server close leaves the transfer waiting on its bidirectional stream until `-m` (exit 28 today). The exit code and text are the case's choice (BL-4608 Notes): 56 as for any session the peer ends early, the message carrying the code and the UTF-8 reason. Depends on BL-4623, which fixes the crash after a session and touches the same files.

## Acceptance criteria

- [ ] `WebTransportCloseTests.ServerClose_WithCodeAndMessage_EndsTheTransferWithExit56AndTheClientSendsOnlyFin` passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.WebTransport.UnitTests` pins the fix (ADR-0482).
- [ ] The `WebTransport:WTH3-6-2 BL-4624` line is removed from `KnownFailures.txt`; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and `Curl.Extended.Protocol.WebTransport.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created.
