---
id: BL-042
title: Implement the gopher and gophers schemes
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-033, BL-034, BL-035]
touches: [Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-042 — Implement the gopher and gophers schemes

## Goal

`GopherProtocolHandler` in `Curl.Protocol.Gopher.UnitLibrary` serves `gopher` and
`gophers`: it sends the selector curl 8.21.0 sends and writes the response to `Output`
unaltered, asking `IConnector` for TLS when the scheme is `gophers`.

## Context

Requirements: the gopher rows BL-033 adds to `Documentation/Product/Requirements.md`.
Seams: ADR-0005 (`IConnector`) and ADR-0006 (`TransferContext`). Per the Product
Overview, `gophers` is gopher over TLS - the same handler with
`ConnectTarget.UseTls = true`, not a second handler.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24) against a
loopback listener; default port 70 for both schemes per
<https://curl.se/docs/url-syntax.html>:

| URL path | Bytes sent |
| --- | --- |
| `/` | `\r\n` |
| `/1` | `\r\n` |
| `/1/foo` | `/foo\r\n` |
| `/0/a%09b` | `/a\tb\r\n` |
| `/7/search%09term%20x` | `/search\tterm x\r\n` |

The selector is the path with its leading `/` and the item-type character removed, then
percent-decoded. The server's reply (`iHello\tfake\t(NULL)\t0\r\n.\r\n` in the capture)
was written to stdout byte for byte, exit 0.

## Acceptance criteria

- [x] `GopherProtocolHandler(IConnector connector)` implements `IProtocolHandler` with
      `SupportedSchemes` exactly `["gopher", "gophers"]` and constructs no `Socket` or
      `SslStream`.
- [x] One test per row of the table asserts the exact bytes written to a scripted fake
      `IConnection` declared in `Curl.Protocol.Gopher.UnitTests`.
- [x] Tests assert `gopher://h/` connects to `ConnectTarget("h", 70, false)`,
      `gophers://h/` to `ConnectTarget("h", 70, true)`, and an explicit port is used.
- [x] A test asserts the scripted reply reaches `Output` byte for byte with exit 0 and
      `BytesTransferred` equal to its length.
- [x] A test asserts a failed `ConnectResult` is returned with its code and message
      unchanged and nothing written.
- [x] A server that closes without sending anything is measured against curl 8.21.0,
      recorded in `Notes`, and pinned by a named test.
- [x] Every test builds its context with `TransferContext`; the test project declares no
      `ITransferContext` implementation, and no test is tagged `Integration`.
- [x] `Curl.Protocol.Gopher.UnitLibrary/CLAUDE.md` names `IConnector` (ADR-0005) as the
      seam instead of a constructor-injected `IConnection`.
- [x] `dotnet build Curl.Protocol.Gopher.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Gopher.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Do not edit `Documentation/Product/Requirements.md`; if a gopher row there is wrong, file
a task. The production TLS provider is not on the board yet; the tests here use a fake
connector, so it is not needed.

Delivered 2026-09-26 (dark factory lane 3):

- `GopherProtocolHandler(IConnector)` connects to port 70 (TLS only for `gophers`),
  sends the selector and CRLF, and copies the reply to `Output` until the server closes.
  `GopherSelector` builds the selector; `GopherTransferMessages` holds curl's messages.
- Measured against curl 8.21.0 and a loopback listener, each pinned by a named test:
  - A server that reads the selector and closes without replying: nothing printed, exit 0
    (`ExecuteAsync_ServerClosesWithoutReplying_SucceedsWithNothingWritten`).
  - `/0/a%00b`: connects, sends nothing, exit 3 "URL using bad/illegal format or missing
    URL".
  - The query is part of the selector and the fragment is not: `/0/a%3fb?x=1#frag` sent
    `/a?b?x=1`, and `?q` with no path sent `q`.
  - curl drops two characters of the still-encoded path: `/%31%2Ffoo` sent `31/foo`.
  - Dot segments are removed first, encoded dots are not: `/0/a/../b%2e%2E/c` sent
    `/b../c`; `/1/a/..` sent `/`.
- Choice: the path is read from `Uri.OriginalString`, not `Uri.PathAndQuery`, because
  .NET unescapes encoded unreserved characters (`%31` became `1`) and gives `gopher` no
  query component, both of which would change which two characters curl drops. Dot
  segments are then removed per RFC 3986 5.2.4 as curl's URL parser does.
- Choice: the two dropped units are UTF-8 bytes, matching curl's byte-level `gopher.c`.
- Failures not measured against a live peer follow the MQTT handler's wording: send
  failure exit 55, receive failure exit 56, output write failure exit 23.
- Review (code-reviewer): no defects; its uncovered-branch and non-ASCII points were
  fixed with tests, and the unused recording fake was replaced by `WriteRefusingStream`.
- Coverage: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Gopher.UnitLibrary` reports
  100% line, 100% branch, worst CRAP 8, no failing member. 35 tests.
- No handler is registered with `Curl.Console` yet, like every other protocol; that is
  not gopher work, so no follow-up was filed here.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. gopher:// and gophers:// send curl 8.21.0's selector through IConnector and write the reply unaltered
