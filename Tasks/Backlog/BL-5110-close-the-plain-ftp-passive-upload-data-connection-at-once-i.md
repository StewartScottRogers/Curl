---
id: BL-5110
title: Close the plain-FTP passive upload data connection at once instead of after 2 seconds
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Networking.UnitLibrary/PooledConnection.cs, Curl.Engine.UnitLibrary/SocketCallbackConnection.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpPassiveUploadDataShutdownTests.cs, Curl.Networking.UnitTests/PooledConnectionShutDownSendingForwardingTests.cs, Curl.Engine.UnitTests/SocketCallbackConnectionShutDownSendingTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5110 — Close the plain-FTP passive upload data connection at once instead of after 2 seconds

## Goal

A plain-FTP passive upload (EPSV/PASV, `-T`) signals end of data by shutting down the data connection's sending side at once, so the upload finishes without waiting out the 2-second `DataShutdownTimeout`, and `-m 1` uploads succeed.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-063 (Low; functional: a `-m 1` upload fails although the server has stored the file). Confirmed twice on Windows and twice on Linux. Area: FTP passive with `-T`. Active mode (`-P -`) and FTPS passive are not affected.

**Repro.**
1. Run an FTP server (the review used pyftpdlib 2.2.0; use a PowerShell or C# file-based stand-in, or any existing server, not a Python script you commit) on 127.0.0.1:23021 with a writable anonymous user and passive ports 23100-23119.
2. `printf 'HELLOWORLD\n' > u.txt`
3. `curl -sS -m 1 -T u.txt ftp://127.0.0.1:23021/up/m1.txt ; echo $?`
4. `curl -v --trace-time -m 20 -T u.txt ftp://127.0.0.1:23021/up/x.txt` and look at the gap after `} [11 bytes data]`.

**Expected (upstream curl).** Step 3 exits 0; the server sees EOF on the data connection right after the 11 bytes.

**Actual.** Step 3 exits 28 with `curl: (28) FTP response timeout` although the server stored the whole file; step 4 shows `upload completely sent off: 11 bytes` exactly 2 s after the data was written. Same on the Linux build.

**Where to start (probable, from code reading).** `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs`, `CloseUploadDataConnectionAsync` (about line 1335) and `ShutDownSendingQuietlyAsync` (about line 2616) call `data.ShutDownSendingAsync` and wait up to `DataShutdownTimeout` (2 s). On the plain passive data connection no FIN is sent: `IConnection.ShutDownSendingAsync` has a no-op default (`Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs`, about line 145) and wrappers such as `Curl.Networking.UnitLibrary/PooledConnection.cs` and `Curl.Engine.UnitLibrary/SocketCallbackConnection.cs` do not forward it. The run decides the fix; forwarding `ShutDownSendingAsync` in those wrappers is the likely one. `touches` covers them, and the run adds any further file it must change to `touches`.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Ftp.UnitTests/FtpPassiveUploadDataShutdownTests.cs` prove that after the last upload byte the session calls `ShutDownSendingAsync` on the data connection and finishes without waiting for `DataShutdownTimeout` (a fake `TimeProvider` is never advanced and the upload completes).
- [ ] Tests in `Curl.Networking.UnitTests/PooledConnectionShutDownSendingForwardingTests.cs` prove `PooledConnection` forwards `ShutDownSendingAsync` to the wrapped connection; a similar test covers `SocketCallbackConnection` in `Curl.Engine.UnitTests/SocketCallbackConnectionShutDownSendingTests.cs`.
- [ ] A regression test proves a data connection whose shutdown is a no-op still ends after `DataShutdownTimeout`, as before.
- [ ] Tests are platform-neutral and need no network.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: step 3 prints `0`, and the server log shows the STOR completing with no 2-second wait.

## Log

- 2026-11-13: Created.
