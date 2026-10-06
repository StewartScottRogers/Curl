---
id: BL-1239
title: Match curl's FTP exit 55 send-failure text and its 421 and refused-EPRT -v lines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1239 — Match curl's FTP exit 55 send-failure text and its 421 and refused-EPRT -v lines

## Goal

An FTP transfer reports a failed send with curl 8.21.0's exit 55 texts, writes `We got a 421 - timeout` before failing on a 421 reply, and writes `disabling EPRT usage` when an `EPRT` is refused and `PORT` follows, as curl does.

## Context

- `Curl.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs` `SendFailed` is `Failure when sending data to the peer`, which is no curl message: curl 8.21.0's `curl_easy_strerror(CURLE_SEND_ERROR)` is `Failed sending data to the peer` (`lib/strerror.c` lines 177-178 at `curl-8_21_0`). It is used by `FtpSession.SendAsync` (a command that cannot be sent) and the upload loop's data-connection write (`FtpSession.cs`, the `catch (IOException)` around `data.WriteAsync`), and pinned by `FtpProtocolHandlerTests.cs` line 688 and `FtpProtocolHandlerUploadTests.cs` line 281. Sibling handlers already give curl's texts and tell a reset apart: `Send failure: Connection was reset` for an `IOException` wrapping `SocketException` `ConnectionReset`, `Failed sending data to the peer` otherwise (`Curl.Protocol.Rtsp.UnitLibrary/RtspIoFailures.cs`, `Curl.Protocol.Dict.UnitLibrary/DictIoFailures.cs`). Copy the pattern, not the type: a protocol library never references another. Check how `FtpControlChannel.TrySendAsync` reports the exception today; it may need to keep it so a reset can be told apart.
- `FtpSession.ReadReplyAsync` fails a 421 with exit 28 but writes no `-v` line first; curl's `ftp_readresp` (`lib/ftp.c` lines 608-619) writes `infof "We got a 421 - timeout"`.
- `FtpSession.AnnounceActivePortAsync(IPAddress, FtpPortArgument)` falls back from a refused `EPRT` to `PORT` writing only a diagnostic-log warning; curl's `ftp_state_port_resp` (`lib/ftp.c` lines 2422-2428) writes `infof "disabling EPRT usage"`.
- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Ftp -FtpIdleMilliseconds 3000 -CurlArgs '-v',...,'ftp://127.0.0.1:<port>/f.txt'`:
  - `-FtpReply 'PWD=421 Timeout'`: stderr ends `> PWD` / `< 421 Timeout` / `* We got a 421 - timeout` / `* closing connection #0` / `curl: (28) Timeout was reached`, exit 28.
  - `-P 127.0.0.1 -FtpReply 'EPRT=500 no'`: `> EPRT |1|127.0.0.1|<port>|` / `< 500 no` / `* disabling EPRT usage` / `* Hostname 127.0.0.1 was found in DNS cache` / `> PORT 127,0,0,1,...` / `< 200 PORT command successful` / `* Connect data stream actively`, exit 0. Pin the `Hostname ... was found in DNS cache` line only if Curl already writes it elsewhere for this case; otherwise leave a note in `Notes` and do not invent it.

## Acceptance criteria

- [x] `FtpTransferMessages` holds curl's two exit 55 texts and no `Failure when sending data to the peer`; tests with a fake connection whose write throws pin exit 55 `Send failure: Connection was reset` for a reset and `Failed sending data to the peer` for any other `IOException`, for a control command and for an upload's data write, with the bytes sent before the failure counted.
- [x] A test pins the measured 421 case: `We got a 421 - timeout` is reported as a `-v` info line before the exit 28 failure, and nothing more is sent.
- [x] A test pins the measured refused-`EPRT` case: `disabling EPRT usage` is reported after the `EPRT` reply and before `PORT` is sent.
- [x] The tests that pinned the old text are updated; every other FTP test passes unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- `FtpControlChannel.TrySendAsync` (bool) became `SendAsync`, returning the `IOException` it caught (or null), so `FtpSession` can tell a reset (`SocketError.ConnectionReset` inner exception) from any other failure. `FtpTransferMessages.SendFailed(IOException)` picks `Send failure: Connection was reset` or `Failed sending data to the peer`, the RTSP/DICT pattern copied, not referenced.
- `We got a 421 - timeout` is written in `FtpSession.ReadReplyAsync` for every 421, as curl's `ftp_readresp` does, whichever exit 28 message follows. `disabling EPRT usage` is written in `WarnEprtRefused`, IPv4 only (IPv6 has no `PORT` fallback, so curl quits with exit 30 there).
- `ExecuteAsync_PortAddressNotLocalAndEprtRefused_RetriesTheBindAgainForPort` pinned the `-v` info lines past the entry path for a refused `EPRT` and now also expects `disabling EPRT usage` between the two not-local lines: curl writes it unconditionally on that path, so the old expectation was incomplete, not a different measurement.
- `* Hostname 127.0.0.1 was found in DNS cache` (measured before `PORT`) is not pinned: Curl writes that line only from `Curl.Networking.UnitLibrary`'s connector/DNS cache, never from the FTP active-mode address lookup, so it would have to be invented here. Left out per the task's Context.
- Coverage: `FtpStateTrace.StateOf`'s `_ => null` arm was uncovered before this task (since `SITE` got its own state, BL-1199). Added `ExecuteAsync_TracedAlternativeToUser_StaysInUserWithNoStateLine` (an unmapped `--ftp-alternative-to-user` verb writes no state line, as curl stays in USER) and `MutableContext.FtpAlternativeToUser` so the library is back at 100%.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. FTP exit 55 says curl's reset or send-failure text; 421 and refused EPRT write curl's -v lines
