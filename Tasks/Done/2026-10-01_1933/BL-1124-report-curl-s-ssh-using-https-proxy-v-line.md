---
id: BL-1124
title: Report curl's SSH using HTTPS proxy -v line for an SSH transfer through an HTTPS proxy
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1123]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1124 — Report curl's SSH using HTTPS proxy -v line for an SSH transfer through an HTTPS proxy

## Goal

Under `-v`, an `sftp://` or `scp://` transfer tunnelled through an HTTPS proxy (`-x https://...`) reports curl 8.21.0's info line `SSH: using HTTPS proxy` right after `SSH: user '<name>'`, as curl's libssh2 back end does; other proxy kinds and direct connections report nothing new.

## Context

- curl 8.21.0, `lib/vssh/libssh2.c` `ssh_connect` (https://github.com/curl/curl/blob/curl-8_21_0/lib/vssh/libssh2.c): line 3477 `SSH: libssh2 cryptography backend: %s`, line 3484 `SSH: user '%s'`, then `if(conn->http_proxy.proxytype == CURLPROXY_HTTPS)` sets libssh2's send and receive callbacks to go through the TLS proxy tunnel and prints `infof(data, "SSH: using HTTPS proxy")` (line 3524 for libssh2 1.11.1, `LIBSSH2_VERSION_NUM >= 0x010b01`, which is the build ADR-0220 matches; line 3558 for older libssh2). The test is on the HTTP proxy's type, so a SOCKS proxy, an `http://` proxy and no proxy print nothing; `--proxy-http2` makes the type `CURLPROXY_HTTPS2`, which does not match either.
- Curl today: `Curl.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs` `RunSessionAsync` reports `SshInfoLines.CryptographyBackend` and `SshInfoLines.User` and nothing about the proxy; `ITransferContext.Proxy` is a `ProxyEndpoint` whose `Kind` is `ProxyKind.Https` for `-x https://`. Add the line to `SshInfoLines` and report it after the user line when the proxy is `ProxyKind.Https` (and not HTTP/2 to the proxy, if `ProxyEndpoint` models `--proxy-http2`; say in Notes which).
- Measure the line's position with real curl if an HTTPS proxy can be stood up on loopback; otherwise the order above, from the source, is what the test pins.

## Acceptance criteria

- [x] New tests in `Curl.Protocol.Ssh.UnitTests` pin, for SFTP and for SCP through a `ProxyKind.Https` proxy, the info lines in order `SSH: user '<name>'` then `SSH: using HTTPS proxy`.
- [x] Tests pin that `ProxyKind.Http`, `ProxyKind.Socks5` and no proxy report no such line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed first as "Send no SFTP read and report curl's line for a download with nothing to fetch"; rewritten before it was committed, because curl turns `-C N` into the range `N-`, so `Curl_ssh_range` refuses a download with nothing left (exit 33, already pinned by `SftpFileDownloadTests.Ranges.cs`) and libssh2.c's `SSH: file already completely downloaded` is unreachable from the tool.

- Done: `SshInfoLines.UsingHttpsProxy`, reported by `SshProtocolHandler.ReportSessionStart` (split out of `RunSessionAsync`, whose complexity reached 14 with the new branch) right after the user line when `ITransferContext.Proxy.Kind` is `ProxyKind.Https`. Tests in `SshProtocolHandlerTests.HttpsProxyLine.cs`.
- `ProxyEndpoint` does not model `--proxy-http2`, so the check is on `ProxyKind.Https` alone; if `--proxy-http2` is modelled later as its own kind, it already reports nothing, matching curl's `CURLPROXY_HTTPS2`.
- Not measured against real curl: no HTTPS proxy that tunnels SSH could be stood up on loopback with the existing tooling, so the position is pinned from libssh2.c `ssh_connect`, as Context allows.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. SFTP and SCP through an HTTPS proxy report curl's 'SSH: using HTTPS proxy' line after the user line
