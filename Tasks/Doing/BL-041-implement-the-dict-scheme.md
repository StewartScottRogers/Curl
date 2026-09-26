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
completed:
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

- [ ] `DictProtocolHandler(IConnector connector)` implements `IProtocolHandler` with
      `SupportedSchemes` exactly `["dict"]`, and constructs no `Socket` or `SslStream`.
- [ ] One test per row of the table in `Context` asserts the exact bytes written to a
      scripted fake `IConnection`, declared in `Curl.Protocol.Dict.UnitTests`.
- [ ] Tests assert `dict://h/d:x` connects to `ConnectTarget("h", 2628, false)` and
      `dict://h:1234/d:x` to port 1234.
- [ ] A test asserts the server's scripted reply reaches `Output` byte for byte, with
      exit 0 and `BytesTransferred` equal to its length.
- [ ] A test asserts a `ConnectResult.Failed(CurlExitCode.CouldntConnect, "m")` is
      returned with the same code and message, and nothing is written to `Output`.
- [ ] The `/define:` and `/match:` spellings, and a server that closes without sending
      anything, are measured against curl 8.21.0 during the run, recorded in `Notes`,
      and pinned by named tests.
- [ ] Every test builds its context with `TransferContext`; the test project declares no
      `ITransferContext` implementation, and no test is tagged `Integration`.
- [ ] `Curl.Protocol.Dict.UnitLibrary/CLAUDE.md` names `IConnector` (ADR-0005) as the
      seam instead of a constructor-injected `IConnection`.
- [ ] `dotnet build Curl.Protocol.Dict.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Dict.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

Open question for Stewart, not blocking: whether the `CLIENT` line should keep naming
`libcurl 8.21.0` (byte-identical to upstream, the default this task takes) or identify
this implementation. Whichever he picks, it is one constant.

Do not edit `Documentation/Product/Requirements.md`; if a `dict://` row there is wrong,
file a task. Registering the handler in `Curl.Console` is end-to-end composition work
and is not in this task.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
