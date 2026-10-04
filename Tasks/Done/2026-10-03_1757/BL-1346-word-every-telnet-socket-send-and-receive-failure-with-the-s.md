---
id: BL-1346
title: Word every telnet socket send failure with the shared CurlSocketErrorText table
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1325]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: FR-085
created: 2026-10-03
completed: 2026-10-03
---
# BL-1346 — Word every telnet socket send failure with the shared CurlSocketErrorText table

## Goal

The telnet handler words every failed socket send with `CurlSocketErrorText` (BL-1325) and keeps no table of its own: `TelnetSocketErrorText` is removed, and the exit 55 text for a failed standard-input send is `Send failure: <words>` for the actual socket error instead of always `Send failure: Connection was reset`.

## Context

- Today `Curl.Protocol.Telnet.UnitLibrary/TelnetSocketErrorText.cs` words only `ConnectionReset` and `ConnectionAborted` on Windows (the exception's message otherwise) for `TelnetOutbox`'s `Sending data failed` / `Send failure:` lines (`TelnetOutbox.cs` line 101). Separately, `TelnetProtocolHandler.cs` line 84 declares `SendFailureMessage = "Send failure: Connection was reset"` and `SendFailure` (line 360) returns it for every failed send, whatever the error and on every platform.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-socket.c` line 1562: every socket send error but `EAGAIN` is `failf(data, "Send failure: %s", curlx_strerror(...))`, with the full Winsock table on Windows (`lib/curlx/strerr.c` `get_winsock_error`) and `strerror`'s words elsewhere; BL-1325's table carries all of them.
- Use `CurlSocketErrorText.Words` / `SendFailure` in both places; where `SendFailure` (line 360) has no exception to word today, pass the caught `IOException` through. An `IOException` with no `SocketException` gets curl_easy_strerror's `Failed sending data to the peer`.
- Existing tests pinning `Connection was reset` hold on Windows only after this change: mark each `[OSCondition(OperatingSystems.Windows)]` and add a non-Windows twin (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) asserting the exception's own message. `TelnetSocketErrorText`'s own tests go with it; the shared table's tests replace them.

## Acceptance criteria

- [x] `Curl.Protocol.Telnet.UnitLibrary/TelnetSocketErrorText.cs` no longer exists, and nothing in the library declares a `Connection was reset` literal.
- [x] A test in `Curl.Protocol.Telnet.UnitTests` makes the standard-input send throw `IOException` wrapping `SocketException(SocketError.ConnectionAborted)` and asserts exit 55 with `Send failure: Connection was aborted` (Windows-only), plus a non-Windows twin with `Send failure: ` + the exception's own message.
- [x] A test pins that a failed negotiation reply wrapping `SocketException(SocketError.NetworkReset)` reports `TelnetOutbox.DescribeSendFailure`'s line as `Send failure: Network has been reset` on Windows (and the exception's own message off Windows), beside the unchanged `Sending data failed (10052)`-style line.
- [x] Every existing telnet test passes, split by platform where it pins `Connection was reset`.
- [x] `dotnet build Curl.Protocol.Telnet.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Telnet.UnitLibrary` reports no failing member.

## Notes

- The two tests in `TelnetProtocolHandlerTests` and the one in `TelnetProtocolHandlerSendFailureTests` that pinned `Connection was reset` used `FaultingConnection`, whose failed write carries no `SocketException`; they now pin curl_easy_strerror's `Failed sending data to the peer` on every platform (the task's own rule), so they need no platform split. The `SocketFailingConnection` upload test is the one split by platform (its error is `ConnectionAborted`).
- `SocketFailingConnection.Failure` became `init`-settable so the `NetworkReset` reply test can choose its error.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Telnet words every socket send failure with CurlSocketErrorText; TelnetSocketErrorText removed
