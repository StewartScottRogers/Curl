---
id: BL-3840
title: CGR-015 part 3: HTTP percent-encodes a URL's path and query, and sends --request-target, in the command-line text encoding
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitTests/Cgr015Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3840 — CGR-015 part 3: HTTP percent-encodes a URL's path and query, and sends --request-target, in the command-line text encoding
# CGR-015 part 3: HTTP percent-encodes a URL's path and query, and sends --request-target, in the command-line text encoding

## Goal

The HTTP handler percent-encodes the URL path and query, and writes `--request-target`, from the bytes of `HttpRequestOptions.CommandLineTextEncoding`, so `http://host/é` is sent as `GET /%E9` on Windows.

## Context

Split from BL-3544 (CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read that task for every repro and the expected bytes). Decision (BL-3544, decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). The existing hooks for that encoding are `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage` (`CommandLineOptions.ConfigFileWireTextEncoding`), `HttpRequestOptions.CommandLineTextEncoding` (set by the engine from `CredentialEncoding.ForPlatform`), and the per-library `SmtpCommandLineText` / `ImapCommandLineText` pattern. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`) rather than reading the host's. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

`HttpUrlText.Path`/`RequestTarget` (`HttpUrlText.cs` line 26 and below) and `HttpRequestHeadFormatter.TargetOf` (around line 259) use UTF-8 today. Pass the options' encoding through. Check what a redirect `Location` with non-ASCII bytes does in real curl and keep it unchanged unless it differs; record which source of URL text uses which encoding in Notes.

## Acceptance criteria

- [ ] With cp1252: `http://127.0.0.1:<port>/é` sends `GET /%E9`, `/café` sends `/caf%E9`, `/“é` sends `/%93%E9`, a query `?q=é` typed in the URL sends `?q=` then the unencoded byte `0xE9`, and `--request-target /é?q=é` sends `/` `0xE9` `?q=` `0xE9` unencoded, as real curl on Windows does (commands and both results in Notes).
- [ ] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [ ] Off Windows (UTF-8) the same commands send the same bytes as before.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

- 2026-10-26 (lane 9, no code changed): measured real curl (Git for Windows ucrt64 curl 8.22.0, the `Record-CurlExchange.ps1` default, cp1252 machine) with `Record-CurlExchange.ps1 -Port 21840 -CurlArgs <args>`, request line of `request.bin`:
  - `-s http://127.0.0.1:21840/é` -> `GET /%E9 HTTP/1.1`
  - `-s http://127.0.0.1:21840/café` -> `GET /caf%E9 HTTP/1.1`
  - `-s http://127.0.0.1:21840/“é` -> `GET /%93%E9 HTTP/1.1`
  - `-s http://127.0.0.1:21840/a?q=é` -> `GET /a?q=` byte `0xE9` ` HTTP/1.1` (the query is NOT percent-encoded, as BL-294 found for 8.21.0; `?q=%E9` in BL-3544 comes from `--url-query`, which BL-3844 covers)
  - `-s --request-target "/é?q=é" http://127.0.0.1:21840/` -> `GET /` byte `0xE9` `?q=` byte `0xE9` ` HTTP/1.1` (sent verbatim in cp1252)
  The original first criterion said the typed query is sent as `?q=%E9`; that contradicts real curl, so the criterion was corrected in Backlog (the board allows criteria changes only there) and the task requeued before any code was edited.
- Plan for the next claim: give `HttpUrlText.Path` and `RequestTarget` an `Encoding` parameter (path runes percent-encoded from that encoding's bytes; query bytes in that encoding, one char per byte), pass `options.CommandLineTextEncoding` from `HttpRequestHeadFormatter.TargetOf`/`AbsoluteForm` and `HttpProtocolHandler.OriginAuthRequestOf` (line ~333), and use `WireText(target, options.CommandLineTextEncoding)` for `--request-target` in `TargetOf`. `HttpProtocolHandler.cs`, `HttpConnectionInfoLines.cs` and `HttpRedirectLocation.cs` also call `HttpUrlText.Path`; a redirect's URL text comes from the server's bytes, not the command line, so check real curl with a non-ASCII `Location` and keep UTF-8 there unless it differs. Adding `HttpProtocolHandler.cs` to `touches` may be needed for the auth request target.

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 9c1acdef a9b9a7ee f4912012 326de9ca.
- 2026-10-26: Doing -> Backlog. Criterion 1 contradicts real curl: a query typed in the URL is sent as the raw ANSI byte (?q=0xE9), not ?q=%E9; corrected the criterion from the measurement, ready to claim again
