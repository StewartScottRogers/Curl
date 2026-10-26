---
id: BL-3841
title: CGR-015 part 4: -w writes its literal text and command-line-derived values in the ANSI code page on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs, Curl.Output.UnitTests/Cgr015Tests.cs, Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3841 — CGR-015 part 4: -w writes its literal text and command-line-derived values in the ANSI code page on Windows
# CGR-015 part 4: -w writes its literal text and command-line-derived values in the ANSI code page on Windows

## Goal

On Windows `-w` writes its literal text, and values that come from command-line text such as `%{url_effective}`, in the ANSI code page, so `-w "é"` writes byte E9.

## Context

Split from BL-3544 (CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read that task for every repro and the expected bytes). Decision (BL-3544, decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). The existing hooks for that encoding are `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage` (`CommandLineOptions.ConfigFileWireTextEncoding`), `HttpRequestOptions.CommandLineTextEncoding` (set by the engine from `CredentialEncoding.ForPlatform`), and the per-library `SmtpCommandLineText` / `ImapCommandLineText` pattern. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`) rather than reading the host's. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

`WriteOutTemplateRenderer.cs` around line 135 encodes the rendered text as UTF-8. Inject the encoding (the Console composition passes the ANSI code page on Windows, UTF-8 elsewhere). A `-w @file` whose bytes are UTF-8 already matches real curl and must keep matching: decide how file bytes and command-line text are kept apart and record it in Notes.

## Acceptance criteria

- [ ] With cp1252: `-o NUL -w "café ü €\n"` writes `63 61 66 E9 20 FC 20 80 0D 0A`, `-w "[é]"` writes `5B E9 5D`, and `%{url_effective}` of `http://127.0.0.1:<port>/café` is written in cp1252 bytes, as real curl on Windows does; `-w @file` with UTF-8 bytes is unchanged (commands and both results in Notes).
- [ ] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [ ] Off Windows (UTF-8) the same commands send the same bytes as before.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
- 2026-10-26: Backlog -> Doing. Criteria at claim: d0c40f12 a9b9a7ee f4912012 326de9ca.
