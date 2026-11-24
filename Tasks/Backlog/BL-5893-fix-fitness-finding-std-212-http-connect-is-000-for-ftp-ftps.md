---
id: BL-5893
title: Fix fitness finding STD-212: %{http_connect} is 000 for FTP, FTPS, SFTP, SCP, DICT, TELNET and GOPHER through --proxytunnel; for DICT, TELNET and GOPHER the CONNECT status lands in %{response_code}
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs, Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs, Curl.Original.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs, Curl.Original.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Engine.UnitLibrary/TransferHeaderStore.cs]
requirement: none
created: 2026-11-24
completed:
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

- [ ] the FTP repro prints `200|226|2`; the DICT repro prints `200|000|000`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
