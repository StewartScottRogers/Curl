---
id: BL-3839
title: CGR-015 part 2: -d family, --json, --data-urlencode, --url-query and --variable encode command-line text in the ANSI code page on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr015Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3839 — CGR-015 part 2: -d family, --json, --data-urlencode, --url-query and --variable encode command-line text in the ANSI code page on Windows
# CGR-015 part 2: -d family, --json, --data-urlencode, --url-query and --variable encode command-line text in the ANSI code page on Windows

## Goal

The `-d`, `--data`, `--data-ascii`, `--data-binary`, `--data-raw`, `--json`, `--data-urlencode`, `--url-query` and `--variable` options turn command-line text into bytes in the ANSI code page on Windows, so `-d é` sends E9 with Content-Length 1.

## Context

Split from BL-3544 (CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read that task for every repro and the expected bytes). Decision (BL-3544, decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). The existing hooks for that encoding are `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage` (`CommandLineOptions.ConfigFileWireTextEncoding`), `HttpRequestOptions.CommandLineTextEncoding` (set by the engine from `CredentialEncoding.ForPlatform`), and the per-library `SmtpCommandLineText` / `ImapCommandLineText` pattern. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`) rather than reading the host's. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

Today they use `Encoding.UTF8.GetBytes` (`CommandLineOptions.AppendPostData` around line 2377, `CommandLineOptionTable.cs` around lines 836, 972 and 979). Use the encoding `ConfigFileWireTextEncoding` already holds on Windows (UTF-8 elsewhere), for text from the command line only: text read from a file (`@file`) stays as its bytes, and text from a UTF-8 config file keeps the behaviour `ReadingConfigFileAsWireText` gives it. MQTT payloads come from the same bytes, so `-d héllo mqtt://...` then sends `68 E9 6C 6C 6F`.

## Acceptance criteria

- [ ] With cp1252: `-d é` posts E9 (Content-Length 1), `--data-urlencode é` posts `%E9`, `--data-urlencode x=é` posts `x=%E9`, `--url-query q=é` adds `?q=%E9`, and `-d €` gives byte 80, as real curl on Windows does (commands and both results in Notes).
- [ ] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [ ] Off Windows (UTF-8) the same commands send the same bytes as before.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
