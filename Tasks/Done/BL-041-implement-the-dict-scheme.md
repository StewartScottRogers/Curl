---
id: BL-041
title: Implement the dict scheme
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-033, BL-034, BL-035]
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-041 — Implement the dict scheme

## Goal

`DictProtocolHandler` in `Curl.Protocol.Dict.UnitLibrary` serves the `dict` scheme: it
sends curl 8.21.0's request bytes over a connection from `IConnector` and writes the
server's reply to `Output` unaltered.

## Context

Requirements: the `dict://` rows BL-033 adds to `Documentation/Product/Requirements.md`.
Seams: ADR-0005 (`IConnector`, `ConnectTarget`, `ConnectResult`) and ADR-0006
(`TransferContext`). The library and its test project exist in `Curl.slnx` and are empty.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24) against a
loopback listener; default port 2628 per <https://curl.se/docs/url-syntax.html>. curl
sends the whole request at once, without waiting for the server's greeting:

| URL path | Bytes sent |
| --- | --- |
| `/d:word` | `CLIENT libcurl 8.21.0\r\nDEFINE ! word\r\nQUIT\r\n` |
| `/d:word:db` | `CLIENT libcurl 8.21.0\r\nDEFINE db word\r\nQUIT\r\n` |
| `/lookup:word` | `CLIENT libcurl 8.21.0\r\nDEFINE ! word\r\nQUIT\r\n` |
| `/m:word:db:prefix` | `CLIENT libcurl 8.21.0\r\nMATCH db prefix word\r\nQUIT\r\n` |
| `/find:word` | `CLIENT libcurl 8.21.0\r\nMATCH ! . word\r\nQUIT\r\n` |
| `/word` | `CLIENT libcurl 8.21.0\r\nword\r\nQUIT\r\n` |
| `/show%20db` | `CLIENT libcurl 8.21.0\r\nshow db\r\nQUIT\r\n` |
| `/` | `CLIENT libcurl 8.21.0\r\n\r\nQUIT\r\n` |

Everything the server sent (`220 hello\r\n150 1 found\r\n...221 bye\r\n` in the capture)
was written to stdout byte for byte, exit 0. The `CLIENT` line names curl's own version;
keep `libcurl 8.21.0` in one constant so it can follow the version this project measures
against (see the question to Stewart recorded in `Notes`).

A connect failure is whatever `ConnectResult.Failed` carries (exit 6 or 7), returned
unchanged with nothing written.

## Acceptance criteria

- [x] `DictProtocolHandler(IConnector connector)` implements `IProtocolHandler` with
      `SupportedSchemes` exactly `["dict"]`, and constructs no `Socket` or `SslStream`.
- [x] One test per row of the table in `Context` asserts the exact bytes written to a
      scripted fake `IConnection`, declared in `Curl.Protocol.Dict.UnitTests`.
- [x] Tests assert `dict://h/d:x` connects to `ConnectTarget("h", 2628, false)` and
      `dict://h:1234/d:x` to port 1234.
- [x] A test asserts the server's scripted reply reaches `Output` byte for byte, with
      exit 0 and `BytesTransferred` equal to its length.
- [x] A test asserts a `ConnectResult.Failed(CurlExitCode.CouldntConnect, "m")` is
      returned with the same code and message, and nothing is written to `Output`.
- [x] The `/define:` and `/match:` spellings, and a server that closes without sending
      anything, are measured against curl 8.21.0 during the run, recorded in `Notes`,
      and pinned by named tests.
- [x] Every test builds its context with `TransferContext`; the test project declares no
      `ITransferContext` implementation, and no test is tagged `Integration`.
- [x] `Curl.Protocol.Dict.UnitLibrary/CLAUDE.md` names `IConnector` (ADR-0005) as the
      seam instead of a constructor-injected `IConnection`.
- [x] `dotnet build Curl.Protocol.Dict.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Dict.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

Open question for Stewart, not blocking: whether the `CLIENT` line should keep naming
`libcurl 8.21.0` (byte-identical to upstream, the default this task takes) or identify
this implementation. Whichever he picks, it is one constant.

Do not edit `Documentation/Product/Requirements.md`; if a `dict://` row there is wrong,
file a task. Registering the handler in `Curl.Console` is end-to-end composition work
and is not in this task.

### Delivered (2026-09-26, dark factory lane 2)

`DictProtocolHandler` connects through `IConnector`, sends the request from
`DictRequest.TryEncode` in one write, then copies every byte read to `Output` until the
server closes. `DictRequest.ClientLine` is the one `CLIENT libcurl 8.21.0` constant.
38 tests in `DictProtocolHandlerTests`; the library is at 100% line and branch coverage.

### Measured against curl 8.21.0 on 2026-09-26 (loopback listener, reply `220 hello...221 bye`)

Every table row in `Context` reproduced exactly. Additionally:

| URL path | Sent command line / result |
| --- | --- |
| `/define:word`, `/define:word:db` | `DEFINE ! word`, `DEFINE db word` (same as `/d:`) |
| `/match:word`, `/match:word:db:prefix` | `MATCH ! . word`, `MATCH db prefix word` (same as `/m:`) |
| `/DEFINE:word`, `/Lookup:word`, `/FIND:word` | prefixes are case-insensitive |
| `/d:`, `/m:` | `DEFINE ! default`, `MATCH ! . default` |
| `/m:x::prefix`, `/m:x:db:`, `/m:word:db` | empty database `!`, empty strategy `.` |
| `/d:word:db:extra` | `DEFINE db word` (fields past the last ignored) |
| `/d:a%3Ab` | `DEFINE b a`: the path is decoded before splitting on `:` |
| `/d:two%20words`, `/d:a'b`, `/d:a%22b%5Cc`, `/d:%C3%A9`, `/d:a%7Fb` | word bytes <= 0x20, >= 0x7F, `'`, `"`, `\` get a backslash; `$` does not |
| `/d:w:a%20b`, `/m:w:a%22b:s%20t` | database and strategy sent raw: `DEFINE a b w`, `MATCH a"b s t w` |
| `/show:db:x`, `/dx:word`, `/define`, `/d` | other paths: sent as-is minus the `/`, each `:` a space |
| `/d:word?q=1`, `/d:word#frag` | query and fragment are not sent |
| `/d:%zz` | `DEFINE ! %zz` (stray `%` kept) |
| `/d:a%01b`, `/show%0Adb`, `/d:a%00b`, `/d:a%0Db` | exit 3 `URL using bad/illegal format or missing URL`, after connecting (a refused port gives 7 instead), nothing sent, nothing written |
| server closes without sending | exit 0, nothing written, request still sent |
| `/d:a`, `/d:x/../y`, `/a/%2e%2e/d:x`, `/d:x/./y` | `DEFINE ! a/b`, `y`, `DEFINE ! x`, `DEFINE ! x/y`: curl normalises backslashes and dot segments as .NET's `Uri` does (measured after code review) |

### Choices taken (unattended run)

- Kept `CLIENT libcurl 8.21.0` byte-identical to upstream, the default the task names;
  the question to Stewart above stays open.
- A path decoding to a control character connects first and then returns exit 3, the
  order curl takes (checked: against a closed port the same URL gives 7).
- The decoder relies on `Uri.AbsolutePath` escaping any stray `%` as `%25` and every
  non-ASCII character as `%XX`, verified on .NET 10; a stray `%` therefore decodes back
  to itself, as curl keeps it.
- Read failures and `Output` write failures still surface as exceptions rather than
  curl's exit 56 and 23; mapping them is outside this task's criteria (see Telnet's
  BL-051 for the same work there).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. dict:// sends curl 8.21.0's DEFINE/MATCH/raw request through IConnector and writes the reply unaltered
