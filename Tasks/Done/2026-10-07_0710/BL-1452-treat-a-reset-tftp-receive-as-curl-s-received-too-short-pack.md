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
completed: 2026-10-07
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

- [x] Tests in `Curl.Protocol.Tftp.UnitTests` give the fake datagram channel receives that throw `SocketException(SocketError.ConnectionReset)` (and `ConnectionRefused`) and pin, for a download and an upload: each failed receive reports `Received too short packet` and resends the request; when the retries run out with no answer the transfer fails with `CurlExitCode.CouldntConnect` and the message `Received too short packet`; no exception escapes.
- [x] Rerunning `curl -v -m 20 tftp://127.0.0.1:9/f` with a rebuilt `Curl.Console` on Windows prints the measured lines and exits 7 (record the output in Notes).
- [x] `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

- Fix: `TftpTimeLimits.ReceiveBeforeAsync` catches `SocketException` with `ConnectionReset`
  or `ConnectionRefused` and returns the sentinel `TftpTimeLimits.RefusedReceive` (compared
  by reference); `TftpDownload.AnswerAsync` and `TftpUpload.AnswerAsync` answer it through
  the existing too-short path before the endpoint pinning, so a refusal pins nothing.
  `Curl.Networking`'s `UdpDatagramChannel` is unchanged. Other socket errors still escape
  (pinned by a test).
- Also fixed on the way: curl's `failf` prints every `Received too short packet` as a `-v`
  line; Curl printed none for a real short datagram either. Both too-short paths now report
  it (`events.InternalError`), as the measured output needs.
- Measured again 2026-10-07, real curl 8.21.0 on Windows: `curl -v -m 20 tftp://127.0.0.1:9/f`
  prints `set timeouts for state 0; Total 19994, retry 5 maxtry 4` and **four** (not three, as
  the Context said: maxtry is 4 at `-m 20`) `* Received too short packet` lines, then
  `* shutting down connection #0` and `curl: (7) Received too short packet`, exit 7. With
  `-T` and no `-m`, maxtry 50 and fifty lines, exit 7.
- Rebuilt `Curl.Console` (Debug), same commands:
  ```
  *   Trying 127.0.0.1:9...
  * Established connection to 127.0.0.1 (127.0.0.1 port 9) from  port 0
  * set timeouts for state 0; Total 19969, retry 5 maxtry 4
  * Received too short packet
  * Received too short packet
  * Received too short packet
  * Received too short packet
  * shutting down connection #0
  curl: (7) Received too short packet
  ```
  exit 7; the upload printed fifty lines and exit 7, as real curl.
- Tests: 240 pass (8 new: download and upload, each with `ConnectionReset` and
  `ConnectionRefused`, plus refusal-then-answer and other-error-escapes). The fake
  `FallsSilentDatagramChannel` gained `Refusal(SocketError)` script entries.
- Measure-CodeQuality: first run flagged `TftpDownload.AnswerAsync` at complexity 11; split
  into `AnswerAsync` (refused or not) and `AnswerDatagramAsync`. Second run: 100% line, 100%
  branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A TFTP transfer to a refusing port ends with exit 7 'Received too short packet' and its -v lines instead of crashing
