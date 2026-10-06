---
id: BL-077
title: Map telnet connection and output failures to curl's exit codes
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-043]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-077 — Map telnet connection and output failures to curl's exit codes

## Goal

When a telnet session's connection read fails, a send to the server fails, or writing to
`Output` fails, `TelnetProtocolHandler` returns curl 8.21.0's exit code and message as a
`TransferResult` instead of letting the exception escape.

## Context

BL-043 left these paths unhandled: an `IOException` from `IConnection.ReadAsync`,
`IConnection.WriteAsync` or `Output.WriteAsync` propagates out of `ExecuteAsync`, and an
upload read failure is ignored (the session keeps receiving). Likely codes are exit 56
(`CURLE_RECV_ERROR`), 55 (`CURLE_SEND_ERROR`) and 23 (`CURLE_WRITE_ERROR`)
(<https://curl.se/libcurl/c/libcurl-errors.html>), but measure each against curl 8.21.0
with a loopback listener that resets the connection, and a closed stdout, before pinning
them. `IConnection`'s contract (ADR-0005) does not say which exception a failed read
throws; read `Curl.Networking.UnitLibrary/StreamConnection.cs` to see what production
throws.

## Acceptance criteria

- [x] A connection read that throws `IOException` returns curl's measured exit code and
      message, pinned by a named test with a fake `IConnection`.
- [x] A send to the server that throws `IOException` returns curl's measured exit code and
      message, pinned by a named test.
- [x] An `Output` write that throws `IOException` returns curl's measured exit code and
      message, pinned by a named test.
- [x] The measurements are recorded in `Notes`.
- [x] `dotnet build Curl.Protocol.Telnet.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Delivered directly rather than through the full `/protocol` stages: the scheme already
exists (BL-043) and this is three error paths in one handler, measured and pinned here.

Measured 2026-09-26 with curl 8.21.0 (x86_64-w64-mingw32, Schannel; the Windows build
`C:\Windows\System32\curl.exe` 8.21.0 agrees on the first case) against a Python
loopback listener; `SO_LINGER {1, 0}` then `close` makes the reset:

| Case | Listener | curl stderr (`-sS`) | Exit |
| --- | --- | --- | --- |
| Read reset | sends `hello\r\n`, resets after 0, 0.5 s, or with nothing sent | none; `hello\r\n` written | 0 |
| Send reset | reads the first stdin line, resets; stdin `a`, 1 s, `b`, 0.5 s, `c` | `curl: (55) Send failure: Connection was reset` | 55 |
| Output closed (`>&-`, or piped to a reader that exits) | sends three lines | `curl: Failed writing body` | 23 |
| Output refuses (`-o /dev/full`) | sends three lines | `curl: (23) client returned ERROR on write of 7 bytes` | 23 |

Choices, with why:

- **Read failure is a close, exit 0.** That is what curl 8.21.0 on Windows does: its
  telnet loop waits on `WSAEnumNetworkEvents` and a reset arrives as `FD_CLOSE`, which
  ends the loop with `CURLE_OK`. Linux curl would report 56 `Recv failure: Connection
  reset by peer`; Windows is the development platform (Product-Overview) and the only
  build measurable here, so its behaviour is pinned.
- **Send failure is 55 `Send failure: Connection was reset`**, the measured Windows
  wording, for both the upload and negotiation replies (both go through curl's
  `send_telnet_data`/`Curl_xfer_send`). The upload runs concurrently, so its failure is
  raced against the pending read and wins even if the server never sends again.
- **Output failure is 23 `Failure writing output to destination, passed <n> returned 0`**,
  the message the File, Gopher and MQTT handlers already return for an `Output` that
  throws (BL-021's measured form), with `<n>` the bytes of data offered. The closed-stdout
  wording (`Failed writing body`, with no `(23)` line) and the `-o` wording come from the
  curl tool's writer, which is `Curl.Console`'s side of the seam, not the handler's.
- An empty data chunk (a read that was all commands) is no longer written to `Output`,
  as curl never calls its writer with zero bytes, so it cannot trip a failing output.
- The upload read failure mentioned in Context stays as BL-043 left it (the session keeps
  receiving); no criterion covers it.

Tests: `ExecuteAsync_ConnectionReadIsReset_EndsWithExit0AndKeepsWhatWasWritten`,
`ExecuteAsync_UploadSendIsReset_ExitsWith55SendFailure`,
`ExecuteAsync_NegotiationReplySendIsReset_ExitsWith55SendFailure`,
`ExecuteAsync_OutputWriteFails_ExitsWith23WriteFailure`, with new fakes
`FaultingConnection` and `FaultingOutputStream`. Telnet library coverage stays 100% line
and branch; 107 telnet tests green.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Returned when the 4-lane shift was stopped to repair task IDs that parallel lanes had duplicated; no lane was working it.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. telnet read resets end with exit 0, failed sends exit 55 'Send failure: Connection was reset', failed output writes exit 23, as measured against curl 8.21.0 on Windows
