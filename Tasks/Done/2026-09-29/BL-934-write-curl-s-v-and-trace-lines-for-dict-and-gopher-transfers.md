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
completed: 2026-09-29
---
# BL-934 — Write curl's -v and --trace lines for DICT and Gopher transfers after connect

## Goal

`dict://` and `gopher://` (and `gophers://`) transfers write curl 8.21.0's `-v` lines and `--trace`/`--trace-ascii` blocks for the request sent and the reply received, not only the connect lines they write today.

## Context

- Audit 2026-09-29, part B: an archived task made dict, gopher, telnet and mqtt write the connect `-v` lines by passing `context.Events` to their `ConnectTarget` (`DictProtocolHandler.cs` line ~59, `GopherProtocolHandler.cs` line ~100). Neither handler reports anything after connecting: no `ReportDataSent` for the DICT command or Gopher selector, no `ReportDataReceived` for the reply, and none of the info lines curl may write for them.
- Where: `Curl.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs`, `DictRequest.cs`; `Curl.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs`.
- Measure first with `Record-CurlExchange.ps1` in its default TCP mode, with `-Response` set to a DICT reply (e.g. `220 ok\r\n150 1 definitions\r\n…\r\n.\r\n250 ok\r\n221 bye\r\n`) or a Gopher menu, and `-Tls` for `gophers`: `-v`, `--trace-ascii -` and `--trace -` for `dict://host/d:word`, `dict://host/m:word:db:strategy`, `dict://host/` (the default `HELP`), `gopher://host/1/`, `gopher://host/0/file` and `gophers://host/1/ -k`. Copy the output into Notes with curl's version and build. Pin only what was measured, including which bytes curl dumps as `Send data` versus `Send header` and whether any `* ` lines follow the connect lines.

## Acceptance criteria

- [x] Measured output for the six cases is copied into Notes.
- [x] `Curl.Protocol.Dict.UnitTests` and `Curl.Protocol.Gopher.UnitTests` pin, through a recording `ITransferEvents`, every measured event after connect, in curl's order.
- [x] Existing tests in both projects pass unmodified.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Dict.UnitLibrary` and `Curl.Protocol.Gopher.UnitLibrary`.

## Notes

Measured 2026-09-29 with curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel, through
`Record-CurlExchange.ps1` (default TCP mode; `-Tls` for gophers). Connect lines are
omitted below (`*   Trying ...`, `* Established connection ...`, plus the two schannel
lines for gophers); they are the connector's and were already pinned.

DICT reply used: `220 ok\r\n150 1 definitions\r\n151 "word" db\r\ntext\r\n.\r\n250 ok\r\n221 bye\r\n` (68 bytes).

`--trace-ascii - dict://127.0.0.1:47934/d:word`:
```
=> Send data, 44 bytes (0x2c)
0000: CLIENT libcurl 8.21.0
0017: DEFINE ! word
0026: QUIT
<= Recv data, 68 bytes (0x44)
0000: 220 ok
...
003b: 221 bye
<= Recv data, 0 bytes (0x0)
* shutting down connection #0
```
`dict://…/m:word:db:strategy`: identical shape, `=> Send data, 53 bytes (0x35)` with
`MATCH db strategy word`. `dict://…/`: `=> Send data, 31 bytes (0x1f)` with an empty
line where the command goes (not `HELP`; the existing `DictRequest` already sends that).
`-v` for all three: `} [44|53|31 bytes data]` (in the progress meter), then
`* shutting down connection #0`. `--trace -` for `d:w` gave the same blocks in hex.
So the request is one `Send data` block (never `Send header`), and no `* ` line
follows the connect lines until `shutting down`.

Gopher menu used: `iHello\tfake\t(NULL)\t0\r\n0file\t/file\t127.0.0.1\t70\r\n.\r\n` (51 bytes).

`--trace-ascii - gopher://127.0.0.1:47935/1/` and `/0/file` (identical):
```
<= Recv data, 51 bytes (0x33)
0000: iHello.fake.(NULL).0
0016: 0file./file.127.0.0.1.70
0030: .
<= Recv data, 0 bytes (0x0)
* shutting down connection #0
```
No `=> Send data` block for the selector at all (the request bytes `/\r\n` and
`/file\r\n` did arrive). `-v`: `{ [51 bytes data]` then `* shutting down connection #0`.

`-k --trace-ascii - gophers://127.0.0.1:47936/1/` (the recorder's TLS server ends without
close_notify), exit 56:
```
<= Recv data, 25 bytes (0x19)
0000: iHello.fake.(NULL).0
0016: .
* schannel: server closed abruptly (missing close_notify)
* closing connection #0
```
and `curl: (56) schannel: server closed abruptly (missing close_notify)` with `-S`.

Also measured, to pin the failure endings: `gopher://…/1a%00b` and `dict://…/d:a%01b`
(exit 3) both write only `* shutting down connection #0` after connecting; gopher with an
output that refused the write (exit 23) wrote `{ [3 bytes data]`, `* client returned ERROR
on write of 3 bytes`, `* closing connection #0`.

Decisions (defaults taken, no ADR needed - they follow the measurements):
- Gopher exit 55 and a plain exit 56 (fallback texts, which curl prints without `failf`)
  report only `closing connection #N`, no message line: inferred from curl's `failf`
  echoing to `-v` and from the measured exit 23 and exit 3 endings; not measured, because
  the recorder cannot make a send fail or reset mid-reply.
- A gophers read that ends without close_notify now fails with the TLS build's own text
  (`MissingCloseNotifyException.Message`), as HTTP does (ADR-0221), instead of the
  fallback text - measured above.
- DICT read failures still propagate as before (unchanged; out of scope).
- The line ending the connection uses `ConnectResult.ConnectionNumber`.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. dict and gopher(s) transfers report curl's -v/--trace data blocks and connection-ending lines after connect
