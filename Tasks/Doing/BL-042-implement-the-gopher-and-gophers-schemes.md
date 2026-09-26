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
completed:
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

- [ ] `GopherProtocolHandler(IConnector connector)` implements `IProtocolHandler` with
      `SupportedSchemes` exactly `["gopher", "gophers"]` and constructs no `Socket` or
      `SslStream`.
- [ ] One test per row of the table asserts the exact bytes written to a scripted fake
      `IConnection` declared in `Curl.Protocol.Gopher.UnitTests`.
- [ ] Tests assert `gopher://h/` connects to `ConnectTarget("h", 70, false)`,
      `gophers://h/` to `ConnectTarget("h", 70, true)`, and an explicit port is used.
- [ ] A test asserts the scripted reply reaches `Output` byte for byte with exit 0 and
      `BytesTransferred` equal to its length.
- [ ] A test asserts a failed `ConnectResult` is returned with its code and message
      unchanged and nothing written.
- [ ] A server that closes without sending anything is measured against curl 8.21.0,
      recorded in `Notes`, and pinned by a named test.
- [ ] Every test builds its context with `TransferContext`; the test project declares no
      `ITransferContext` implementation, and no test is tagged `Integration`.
- [ ] `Curl.Protocol.Gopher.UnitLibrary/CLAUDE.md` names `IConnector` (ADR-0005) as the
      seam instead of a constructor-injected `IConnection`.
- [ ] `dotnet build Curl.Protocol.Gopher.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Gopher.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Do not edit `Documentation/Product/Requirements.md`; if a gopher row there is wrong, file
a task. The production TLS provider is not on the board yet; the tests here use a fake
connector, so it is not needed.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
