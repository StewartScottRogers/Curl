---
id: BL-934
title: Write curl's -v and --trace lines for DICT and Gopher transfers after connect
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-934 — Write curl's -v and --trace lines for DICT and Gopher transfers after connect

## Goal

`dict://` and `gopher://` (and `gophers://`) transfers write curl 8.21.0's `-v` lines and `--trace`/`--trace-ascii` blocks for the request sent and the reply received, not only the connect lines they write today.

## Context

- Audit 2026-09-29, part B: an archived task made dict, gopher, telnet and mqtt write the connect `-v` lines by passing `context.Events` to their `ConnectTarget` (`DictProtocolHandler.cs` line ~59, `GopherProtocolHandler.cs` line ~100). Neither handler reports anything after connecting: no `ReportDataSent` for the DICT command or Gopher selector, no `ReportDataReceived` for the reply, and none of the info lines curl may write for them.
- Where: `Curl.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs`, `DictRequest.cs`; `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs`.
- Measure first with `Record-CurlExchange.ps1` in its default TCP mode, with `-Response` set to a DICT reply (e.g. `220 ok\r\n150 1 definitions\r\n…\r\n.\r\n250 ok\r\n221 bye\r\n`) or a Gopher menu, and `-Tls` for `gophers`: `-v`, `--trace-ascii -` and `--trace -` for `dict://host/d:word`, `dict://host/m:word:db:strategy`, `dict://host/` (the default `HELP`), `gopher://host/1/`, `gopher://host/0/file` and `gophers://host/1/ -k`. Copy the output into Notes with curl's version and build. Pin only what was measured, including which bytes curl dumps as `Send data` versus `Send header` and whether any `* ` lines follow the connect lines.

## Acceptance criteria

- [ ] Measured output for the six cases is copied into Notes.
- [ ] `Curl.Protocol.Dict.UnitTests` and `Curl.Protocol.Gopher.UnitTests` pin, through a recording `ITransferEvents`, every measured event after connect, in curl's order.
- [ ] Existing tests in both projects pass unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Dict.UnitLibrary` and `Curl.Protocol.Gopher.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
