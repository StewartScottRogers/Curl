---
id: BL-1325
title: Add one shared table that words a socket error as curl's 'Recv failure:' and 'Send failure:' lines on each platform
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: FR-085
created: 2026-10-03
completed:
---
# BL-1325 — Add one shared table that words a socket error as curl's 'Recv failure:' and 'Send failure:' lines on each platform

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` has one public static class, `CurlSocketErrorText`, that turns a failed read or write into curl 8.21.0's exit 56 / exit 55 text, `Recv failure: <words>` / `Send failure: <words>`, with the words curl's build for the platform uses: the Schannel build's own Winsock table on Windows, the C library's `strerror` elsewhere. Every protocol library can then stop keeping its own copy (the tasks that depend on this one adopt it, one library each).

## Context

- Today each handler keeps its own constants, and only for a reset: `"Recv failure: Connection was reset"` / `"Send failure: Connection was reset"` in `DictIoFailures`, `FtpTransferMessages`, `GopherTransferMessages`, `HttpTransferMessages`, `MqttTransferMessages`, `Pop3SessionMessages`, `RtspIoFailures`, `SmbMessages`, `SmtpSessionMessages`, `TelnetProtocolHandler`, `WsIoFailures`; any other socket error, an aborted connection included, falls back to curl_easy_strerror's `Failure when receiving data from the peer` / `Failed sending data to the peer`. `Curl.Protocol.Telnet.UnitLibrary/TelnetSocketErrorText.cs` already does the platform split for two errors, and `Curl.Networking.UnitLibrary/TlsFailureMessages.cs` (`SchannelSocketErrorTexts`, `RecvFailure`, lines 107-119 and 785-790) does it for the handshake. This is the one place the table should live: protocol libraries reference only Abstractions (root `CLAUDE.md`), so it cannot live in Networking.
- curl 8.21.0 (tag `curl-8_21_0`): `lib/cf-socket.c` writes `failf(data, "Send failure: %s", curlx_strerror(sockerr, ...))` (line 1562, `CURLE_SEND_ERROR`) and `failf(data, "Recv failure: %s", ...)` (line 1618, `CURLE_RECV_ERROR`) for every socket error that is not `EAGAIN`. `lib/curlx/strerr.c` `curlx_strerror` (lines 248-300): on Windows `strerror_s`, then, when that says `Unknown error`, `get_winsock_error` (lines 44-227); elsewhere `strerror_r`.
- The Winsock words, from `get_winsock_error`, for the errors .NET names in `System.Net.Sockets.SocketError`: `ConnectionAborted` `Connection was aborted`, `ConnectionReset` `Connection was reset`, `NetworkReset` `Network has been reset`, `NotConnected` `Socket is not connected`, `Shutdown` `Socket has been shut down`, `TimedOut` `Timed out`, `ConnectionRefused` `Connection refused`, `NetworkDown` `Network down`, `NetworkUnreachable` `Network unreachable`, `HostDown` `Host down`, `HostUnreachable` `Host unreachable`, `NoBufferSpaceAvailable` `No buffer space`. Copy each from the source file when implementing and cite the line in the type's remarks; any other error uses `SocketException.Message`.
- Off Windows, .NET's `SocketException.Message` is the platform's `strerror` text (`Connection reset by peer`, `Software caused connection abort` on Linux), which is what curl's OpenSSL build prints, so the table there is the exception's own message. Decide the platform with `OperatingSystem.IsWindows()` behind an overload that takes it as a parameter, as `TelnetSocketErrorText.For(SocketException, bool)` does, so both platforms' words are testable everywhere.
- Shape (adjust names only if `Documentation/Wiki/Glossary.md` already names the concept): `string? ReceiveFailure(IOException)` and `string? SendFailure(IOException)` return the full `Recv failure: ...` / `Send failure: ...` text when the exception (or an inner exception) is a `SocketException`, else `null`, so a caller keeps its own fallback text; plus `string Words(SocketException, bool isWindows)`.

## Acceptance criteria

- [ ] `Curl.Protocol.Abstractions.UnitLibrary/CurlSocketErrorText.cs` exists with the members above and XML doc comments that cite `lib/cf-socket.c` lines 1562 and 1618 and `lib/curlx/strerr.c` `get_winsock_error`.
- [ ] A data-driven test in `Curl.Protocol.Abstractions.UnitTests` pins `Words(new SocketException((int)error), isWindows: true)` for each of the twelve errors listed in Context, and that an unlisted error (e.g. `SocketError.AccessDenied`) gives the exception's own message.
- [ ] A test pins that `isWindows: false` gives `SocketException.Message` for `ConnectionReset` and `ConnectionAborted`.
- [ ] Tests pin `ReceiveFailure(new IOException("x", new SocketException((int)SocketError.ConnectionAborted)))` is `Recv failure: Connection was aborted` on Windows (via the overload taking the platform), `SendFailure` likewise with `Send failure: `, and that an `IOException` with no socket error inside returns `null`.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Abstractions.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Blocked. Stewart: dark factory timed out after 120 min; see Z:\repos\Curl.logs\BL-1325-20261003-061205-L2.jsonl
- 2026-10-03: Blocked -> Doing.
