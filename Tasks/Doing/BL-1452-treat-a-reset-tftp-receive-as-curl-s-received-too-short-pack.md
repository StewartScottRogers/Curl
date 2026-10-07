---
id: BL-1452
title: Treat a reset TFTP receive as curl's 'Received too short packet' instead of crashing
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: FR-032
created: 2026-10-04
completed:
---
# BL-1452 — Treat a reset TFTP receive as curl's 'Received too short packet' instead of crashing

## Goal

A TFTP transfer to a port where nothing listens ends as curl 8.21.0's does on Windows - three `* Received too short packet` lines under `-v` and `curl: (7) Received too short packet`, in about a second - instead of crashing with `Unhandled exception. System.Net.Sockets.SocketException (10054): An existing connection was forcibly closed by the remote host.`

## Context

- Measured 2026-10-04 on Windows: `curl -v -m 20 tftp://127.0.0.1:9/f` (real curl 8.21.0, Schannel) prints `Trying`, `Established connection ...`, then `* Received too short packet` three times, `* shutting down connection #0` and `curl: (7) Received too short packet`, exit 7, after 1.2 s. `Curl.Console` with the same URL (download, and `-T <file>` upload) dies with the unhandled `SocketException` (WSAECONNRESET), thrown by `Curl.Networking.UnitLibrary/UdpDatagramChannel.cs` `ReceiveAsync` (line 166) through `Curl.Protocol.Tftp.UnitLibrary/TftpTimeLimits.cs` `ReceiveBeforeAsync` (line 88) and `TftpDownload.RunAsync` (line 106).
- Why: on Windows an ICMP port-unreachable for a sent UDP datagram makes the next `recvfrom` fail with WSAECONNRESET. Upstream (tag `curl-8_21_0`) `lib/tftp.c` `tftp_receive_packet` lines 1046-1074: `recvfrom` returns -1, so `rbytes < 4`, and curl writes `failf("Received too short packet")` and treats it as `TFTP_EVENT_TIMEOUT` - the request is resent and the retry count rises; when the retries run out before any answer, the transfer fails with exit 7, and the first `failf` text is the one `curl: (7)` shows.
- Curl already answers a datagram shorter than 4 bytes this way (`TftpDownload.AnswerTooShortAsync`, `TftpUpload`'s `tooShortReceived`); a receive that fails with `SocketError.ConnectionReset` should take the same path. Fix it in the TFTP library (catch at the receive), not in `Curl.Networking`, so the datagram channel's contract is unchanged; say so in the remarks of the member that catches it. Off Windows, Linux reports ECONNREFUSED for a connected UDP socket instead; handle `SocketError.ConnectionRefused` the same way.
- BL-1435 (unexpected opcodes, done) changed the same receive loop; build on its code.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Tftp.UnitTests` give the fake datagram channel receives that throw `SocketException(SocketError.ConnectionReset)` (and `ConnectionRefused`) and pin, for a download and an upload: each failed receive reports `Received too short packet` and resends the request; when the retries run out with no answer the transfer fails with `CurlExitCode.CouldntConnect` and the message `Received too short packet`; no exception escapes.
- [ ] Rerunning `curl -v -m 20 tftp://127.0.0.1:9/f` with a rebuilt `Curl.Console` on Windows prints the measured lines and exits 7 (record the output in Notes).
- [ ] `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
