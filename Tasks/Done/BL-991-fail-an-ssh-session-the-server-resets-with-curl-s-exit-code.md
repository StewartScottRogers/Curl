---
id: BL-991
title: Fail an SSH session the server resets with curl's exit code instead of an unhandled IOException
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-991 — Fail an SSH session the server resets with curl's exit code instead of an unhandled IOException

## Goal

An `sftp://` or `scp://` transfer whose server resets or aborts the TCP connection during the SSH handshake (or later) ends with the exit code and message the reference curl gives, not with an unhandled `System.IO.IOException` and its stack trace.

## Context

- Found in BL-575 (2026-09-29): an OpenSSH 10 `sshd` in WSL whose pre-auth child failed to start (exit 127, `libwrap.so.0` missing) reset each connection straight after accept. `curl.exe -v --compressed-ssh -k --key <key> sftp://127.0.0.1:2275/...` printed `* SSH: user '...'`, then `Unhandled exception. System.IO.IOException: Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.` (inner `SocketException` 10053) out of `CurlCommandRunner.TransferAllGroupsAsync`.
- `SshTransport.NegotiateAlgorithmsAsync` maps only `InvalidDataException` and `EndOfStreamException`; `SshIdentificationExchange` and the later reads let `IOException` through. `SshUserAuthentication.TryExchangeAsync` already catches `IOException`.
- Measure first: run the reference curl (Windows mingw build, `Record-CurlExchange.ps1` finds it) against a listener that accepts and resets (extend `Record-CurlExchange.ps1` with a reset-on-accept mode if it has none) and record exit code and stderr; libssh2 probably reports `Failure establishing ssh session: -13, Failed getting banner` (exit 2), but pin what is measured.

## Acceptance criteria

- [x] The reference curl's exit code and stderr for a connection reset before the banner, and after it, are measured and copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` has a test per measured case where the fake `IConnection` throws `IOException` from a read, pinning the exit code and message.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-30, Windows reference build (curl 8.21.0, libssh2 1.11.1), `Record-CurlExchange.ps1 ... -CurlArgs "-sS,-k,-u,user:pw,sftp://127.0.0.1:<port>/file"`; `-Reset` already existed, the other cases use `-Script` (`send`, `read`, `reset`):
  - Reset on accept (`-Reset`, sftp and scp; script `reset`; script `send SSH-2.0-Open` then `reset`): `curl: (2) Failure establishing ssh session: -43, Failed getting banner`, exit 2.
  - Reset after the server read curl's identification (script `read 5` or `read`, then `reset`; also after a banner without CR LF): `curl: (2) Failure establishing ssh session: -13, Failed getting banner`, exit 2.
  - Reset after a full banner, while curl waits for KEXINIT (script `send SSH-2.0-OpenSSH_9.6\r\n`, `read`, `reset`): `curl: (2) Failure establishing ssh session: -1, Unable to exchange encryption keys`, exit 2 - the same as `close` there.
- Decision (ADR-0283): any `IOException` in the identification exchange is `-43, Failed getting banner` (libssh2's receive-error code, and BL-575's reset-on-accept case); the `-13` late-reset variant depends on timing the client cannot see, so Curl prints `-43` there. From the banner to the server's KEXINIT (the client KEXINIT write moved inside the catch) a reset is `-1`; during the key exchange `-8`, as the measured close.
- Re-ran both main cases with the built `Curl.Console\bin\Debug\net10.0\curl.exe` through the recorder: `-43, Failed getting banner` and `-1, Unable to exchange encryption keys`, exit 2, stderr identical to the reference.
- Tests: `Fakes/ResettingConnection` (throws the `NetworkStream` reset `IOException` once its reads run out, optionally on write); `SshIdentificationExchangeTests.ExchangeAsync_PeerResetsTheConnection_FailsWithMinus43AsMeasured` (3 rows), `SshTransportTests.NegotiateAlgorithmsAsync_PeerResetsTheConnectionAfterTheBanner_FailsWithMinus1AsMeasured`, `ExchangeKeysAsync_PeerResetsTheConnectionBeforeTheReply_FailsWithMinus8AsAClose`, `SshProtocolHandlerTests.ExecuteAsync_ServerResetsTheConnection_FailsWithExit2AsMeasured` (sftp and scp).
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0283 and its README row; no task in Doing names it.
- Follow-up filed: BL-1046, a reset after the key exchange (service request, SFTP start-up, transfers), where `SshConnectionFailure.Is` must not swallow a local output `IOException`.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Ssh 1420); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. An SSH server that resets the connection during the handshake now fails with exit 2 and libssh2's message instead of an unhandled IOException
