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

- [ ] With cp1252: `http://127.0.0.1:<port>/é` sends `GET /%E9`, `/café` sends `/caf%E9`, `/“é` sends `/%93%E9`, and a query `?q=é` typed in the URL sends `?q=%E9`, as real curl on Windows does (commands and both results in Notes).
- [ ] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [ ] Off Windows (UTF-8) the same commands send the same bytes as before.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
