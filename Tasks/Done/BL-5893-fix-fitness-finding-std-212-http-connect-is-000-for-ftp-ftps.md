---
id: BL-5893
title: Fix fitness finding STD-212: %{http_connect} is 000 for FTP, FTPS, SFTP, SCP, DICT, TELNET and GOPHER through --proxytunnel; for DICT, TELNET and GOPHER the CONNECT status lands in %{response_code}
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5915]
touches: [Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.ProxyConnect.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.OsErrno.cs, Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs, Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs, Curl.Engine.UnitTests/Std212Tests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5893 — Fix fitness finding STD-212: %{http_connect} is 000 for FTP, FTPS, SFTP, SCP, DICT, TELNET and GOPHER through --proxytunnel; for DICT, TELNET and GOPHER the CONNECT status lands in %{response_code}

## Goal

STD-212 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: %{http_connect} is 000 for FTP, FTPS, SFTP, SCP, DICT, TELNET and GOPHER through --proxytunnel; for DICT, TELNET and GOPHER the CONNECT status lands in %{response_code}. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed · **Area:** write-out over an HTTP CONNECT tunnel, non-HTTP protocols. Windows build.
- **Mocks:** an HTTP proxy on 127.0.0.1:35080 that tunnels CONNECT (and answers other methods with 200); an FTP server on 127.0.0.1:35021 (user `x`, password `p`, file `ten.txt`); optionally an SFTP server on 127.0.0.1:35122 (user `pw`, password `secret`).
- **Repro:**
  - `curl -sS -x http://127.0.0.1:35080 --proxytunnel -o NUL -w "%{http_connect}|%{response_code}|%{num_connects}\n" ftp://x:p@127.0.0.1:35021/ten.txt`
  - `curl -sS -x http://127.0.0.1:35080 --proxytunnel -o NUL -w "%{http_connect}|%{response_code}\n" -u pw:secret --hostpubsha256 <fp> sftp://127.0.0.1:35122/ten.txt`
  - `curl -sS -m 5 -x http://127.0.0.1:35080 -p -o NUL -w "%{http_connect}|%{response_code}|%{http_code}\n" dict://127.0.0.1:35080/x`
- **Expected (REF 8.22.0):** FTP `200|226|2`; FTP with `-P -` `200|226|1`; FTPS (`--ssl-reqd`) `200|226|2`; SFTP and SCP `200|000|1`; DICT, TELNET, GOPHER (time out, exit 28 in both) `200|000|000`.
- **Actual (.NET):** FTP `000|226|2`; FTP `-P -` `000|226|1`; FTPS `000|226|2`; SFTP/SCP `000|000|1`; DICT/TELNET/GOPHER `000|200|200`. Exit codes, stdout and tunnel traffic match. HTTP targets are correct (`200|200`).
- **Likely location:** only `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` copies `ConnectResult.ProxyConnectResponseCode` into `TransferReport.ProxyConnectResponseCode` (mapped to `%{http_connect}` in `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs`); the FTP, SSH, DICT, TELNET and GOPHER handlers never set it. The DICT/TELNET/GOPHER `response_code=200` likely comes from the CONNECT status line being recorded as a response header (`Curl.Engine.UnitLibrary/TransferHeaderStore.cs`, `ManagedEasyTransfer.Finish.cs` `ResponseCodeOf`). Carry the connect code through a shared path for every protocol.
- **Acceptance check:** the FTP repro prints `200|226|2`; the DICT repro prints `200|000|000`.

## Acceptance criteria

- [x] the FTP repro prints `200|226|2`; the DICT repro prints `200|000|000`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-11-24 (lane 13): reproduced and diagnosed; no code changed. Requeued only because the
  test belongs in `Curl.Engine.UnitTests`, which BL-5915 (in Doing) names whole.
- Measured through a loopback proxy that answers `CONNECT` with `200` and then stays silent
  (`-sS -m 2 -x ... -p -w "%{http_connect}|%{response_code}|%{http_code}|%{num_connects}"`):
  Curl prints `000|200|200|1` (exit 28) for `dict://`, `gopher://` and `telnet://`;
  `C:\Windows\System32\curl.exe` prints `200|000|000|1` (exit 28) for `dict://` and
  `gopher://` (its Telnet run did not end within 10 s with standard input redirected).
- Fix, one shared engine path, no handler or `TransferHeaderStore` edits (touches changed to match):
  1. `%{http_connect}`: `EndPointRecordingConnector` hands each connect result's non-zero
     `ConnectResult.ProxyConnectResponseCode` (success and failure, `ConnectAsync` and
     `ConnectMultiplexedSessionAsync`) to `ConnectionEndPointRecorder`, which keeps the last,
     clears it in `Clear`, and puts it on `TransferReport.ProxyConnectResponseCode` when the
     handler's report has 0, building a report as `WithConnectFailure` does. The cancellation
     path in `EndPointReportingProtocolHandler` already calls `ReportOn`, so a `-m` timeout gets
     it too. Check `ManagedEasyTransfer.NumConnectsOf` for a refused tunnel on a non-HTTP scheme.
  2. `%{response_code}` 200: not `TransferHeaderStore`. With `-m` or `-Y`,
     `AbortedHeadRecordingTransferEvents` takes the status of the `CONNECT` reply lines that
     `ConnectTunnelVerboseLines.ReportReplyLine` reports as response headers. They come before
     `ReportConnectionOpened`, so reset `responseCode` and `httpVersion` there (keep
     `headerSize`).

- 2026-11-24 (lane 11): implemented the plan above. `EndPointRecordingConnector` hands every connect result's
  `ProxyConnectResponseCode` to `ConnectionEndPointRecorder.RecordProxyConnect` (new `.ProxyConnect.cs`), which
  puts it on the report when the handler gave none; `AbortedHeadRecordingTransferEvents.ReportConnectionOpened`
  forgets the CONNECT reply's status. Measured with this build through a loopback tunnelling proxy: FTP
  `200|226|2` (exit 0); DICT, GOPHER and TELNET `200|000|000|1` (exit 28), as curl 8.22.0. Tests: `Std212Tests`
  (7). Build clean, fast tests green, Measure-CodeQuality for Curl.Engine.UnitLibrary 0 failing members. The tests are
  pure in-memory (no path, OS error or certificate), so they are platform-neutral.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: ca22b4f3 69116afe 3d836d5e.
- 2026-11-24: Doing -> Backlog. Waits for BL-5915, which holds Curl.Engine.UnitTests; diagnosed and planned in Notes, no code changed.
- 2026-11-24: Backlog -> Doing. Criteria at claim: ca22b4f3 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. %{http_connect} is the CONNECT status for every scheme through -p; DICT/TELNET/GOPHER no longer report it as %{response_code}
