---
id: BL-5904
title: Word a failed dns:// UDP send or receive as curl's Send failure and Recv failure
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/DnsUdpTransportTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5904 — Word a failed dns:// UDP send or receive as curl's Send failure and Recv failure

## Goal

A `dns://` query over UDP whose send or receive fails with a socket error keeps exit 55 or 56 and words it as curl does: `Send failure: <words>` / `Recv failure: <words>`.

## Context

Found by BL-5874. `Transport/Udp/DnsUdpTransport.cs` already gives the right exit codes, but its messages are `Failed sending the query over UDP: <SocketException.Message>` and `Failed receiving the answer over UDP: <SocketException.Message>`, so .NET's wording of the socket error reaches stderr on Windows. `CurlSocketErrorText.Words` (`Curl.Protocol.Abstractions`) gives curl's words per platform.

## Acceptance criteria

- [ ] A test-double channel whose send throws a `SocketException` exits 55 with `Send failure: ` and curl's words for the platform, pinned by unit tests for Windows and non-Windows.
- [ ] The same for a receive, with exit 56 and `Recv failure: `.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 61a38b5a ff8705aa 3d836d5e.
