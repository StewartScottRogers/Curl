---
id: BL-3841
title: CGR-015 part 4: -w writes its literal text and command-line-derived values in the ANSI code page on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3844]
touches: [Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs, Curl.Output.UnitTests/Cgr015Tests.cs, Curl.Console/CurlComposition.cs, Curl.Console/CurlCommandRunner.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/WriteOutFileWireTextTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3841 — CGR-015 part 4: -w writes its literal text and command-line-derived values in the ANSI code page on Windows
# CGR-015 part 4: -w writes its literal text and command-line-derived values in the ANSI code page on Windows

## Goal

On Windows `-w` writes its literal text, and values that come from command-line text such as `%{url_effective}`, in the ANSI code page, so `-w "é"` writes byte E9.

## Context

Split from BL-3544 (CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read that task for every repro and the expected bytes). Decision (BL-3544, decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). The existing hooks for that encoding are `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage` (`CommandLineOptions.ConfigFileWireTextEncoding`), `HttpRequestOptions.CommandLineTextEncoding` (set by the engine from `CredentialEncoding.ForPlatform`), and the per-library `SmtpCommandLineText` / `ImapCommandLineText` pattern. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`) rather than reading the host's. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

`WriteOutTemplateRenderer.cs` around line 135 encodes the rendered text as UTF-8. Inject the encoding (the Console composition passes the ANSI code page on Windows, UTF-8 elsewhere). A `-w @file` whose bytes are UTF-8 already matches real curl and must keep matching: decide how file bytes and command-line text are kept apart and record it in Notes.

## Acceptance criteria

- [x] With cp1252: `-o NUL -w "café ü €\n"` writes `63 61 66 E9 20 FC 20 80 0D 0A`, `-w "[é]"` writes `5B E9 5D`, and `%{url_effective}` of `http://127.0.0.1:<port>/café` is written in cp1252 bytes, as real curl on Windows does; `-w @file` with UTF-8 bytes is unchanged (commands and both results in Notes).
- [x] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [x] Off Windows (UTF-8) the same commands send the same bytes as before.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

- 2026-10-26 (lane 8, first claim, no code edited): requeued behind BL-3844, which holds `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`. That file's `SetWriteOut` is where `-w @file` is read (`Encoding.UTF8.GetString(...)`), and it is the only place that can tell file bytes from command-line text: by the time `options.WriteOut` reaches the Console it is one string with no record of where it came from. Added it, and a new Cli test file, to `touches`.
- Plan (decided, for the next claim):
  1. `WriteOutTemplateRenderer` takes an `Encoding textEncoding` constructor parameter. `Encode` and `EncodeWithRawBytes` encode with it rather than `Encoding.UTF8`; header bytes marked by `MarkHeaderBytes` stay raw. Test in `Curl.Output.UnitTests/Cgr015Tests.cs` with cp1252 from `CodePagesEncodingProvider.Instance.GetEncoding(1252)`, injected so it is platform-neutral: `café ü €
` gives `63 61 66 E9 20 FC 20 80 0A` (CR LF when `writesLineFeedAsCrLf`), `[é]` gives `5B E9 5D`, and with UTF-8 the bytes are unchanged.
  2. `CurlCommandRunner`'s `writeOutRenderer` field passes `runsOnWindows ? ConfigFileWireText.WindowsAnsiCodePage(() => CodePagesEncodingProvider.Instance.GetEncoding(0)) : Encoding.UTF8`, the same hook the parser uses for `ConfigFileWireTextEncoding` (it is a primary-constructor parameter, line 299). `CurlComposition` probably needs no change.
  3. Keep file bytes apart from command-line text with the existing BL-1848 mechanism: in `SetWriteOut`, a `-w @file` template becomes `ConfigFileWireText.Respell(text, options.ConfigFileWireTextEncoding)` when that is set (Windows), so encoding it in the ANSI code page gives the file's own UTF-8 bytes back; the inline value goes through `options.AsWireText(value)`, so a `-w` in a UTF-8 `-K` file keeps its bytes too, as curl uses config-file bytes raw. Off Windows `ConfigFileWireTextEncoding` is null, so nothing changes.
  4. Still to do on that claim: measure with `Record-CurlExchange.ps1` (`-o NUL -w "café ü €
"`, `-w "[é]"`, `%{url_effective}` of `http://127.0.0.1:<port>/café`, `-w @file` with UTF-8 bytes), write the ADR, then run `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary,Curl.Console,Curl.Cli.UnitLibrary`.
- 2026-10-26 (lane 2, second claim): measured real curl (Git for Windows ucrt64 curl 8.22.0, cp1252) with `Record-CurlExchange.ps1 -Port 48641 -CurlArgs -s,-o,NUL,-w,<template>,http://127.0.0.1:48641/café`: `-w "café ü €`n"` gave `63 61 66 E9 20 FC 20 80 0D 0A`; `-w "[é]"` gave `5B E9 5D`; `-w "%{url_effective}"` gave `http://127.0.0.1:48641/caf` + `E9`; `-w @wo.txt` with `wo.txt` = `5B C3 A9 5D` gave `5B C3 A9 5D`. Curl's results are pinned with cp1252 injected in `Curl.Output.UnitTests/Cgr015Tests.cs` (all four, the `@file` one as the re-spelled template the parser hands over) and `Curl.Cli.UnitTests/WriteOutFileWireTextTests.cs` (the parser re-spells `-w @file` and a `-w` in a UTF-8 `-K` file so their bytes come back; an argument stays as typed; off Windows the file is plain UTF-8). Running the built console through the recorder was not possible here: lanes may not read `bin/`.
- Plan followed as written (ADR-0828). The renderer's 5-argument constructor is new and the old 4-argument one keeps UTF-8, so the existing renderer tests did not change; `Cgr015Tests` does not compile without the change, and the Cli `@file` test fails without it (cp1252 would write `5B E9 5D`). Off Windows: `ConfigFileWireTextEncoding` is null and the runner passes UTF-8, so bytes are as before (`RenderAsync_Utf8_WritesTheSameBytesAsBefore`, `Parse_WriteOutFileOffWindows_ReadsTheFileAsUtf8`); CI on Linux and macOS is met by keeping the tests platform-neutral (encoding injected).
- Upstream test2046 and test2047 (IDN URL given as UTF-8 bytes, printed by `%{url_effective}`) failed on Windows after the change, since upstream's runner assumes UTF-8 argv. Added `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs` to touches (no task in Doing on work/dark-factory names it) and hold them off the Windows ratchet only (`NeedsUtf8ArgumentsCases`), as `NeedsTls13ServerCases` does for macOS; Linux and macOS still judge them.
- The runner's constructor reached complexity 14 with the encoding ternary; the renderer's construction moved to the static `CreateWriteOutRenderer`. `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary,Curl.Console,Curl.Cli.UnitLibrary` then `-Library Curl.Console`: 0 failing members. Fast tests: 44121 across 46 projects, 0 failed.

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
- 2026-10-26: Backlog -> Doing. Criteria at claim: d0c40f12 a9b9a7ee f4912012 326de9ca.
- 2026-10-26: Doing -> Backlog. Needs Curl.Cli.UnitLibrary/CommandLineOptionTable.cs (SetWriteOut, where -w @file is read), held by BL-3844 in Doing; depends-on BL-3844
- 2026-10-26: Backlog -> Doing. Criteria at claim: d0c40f12 a9b9a7ee f4912012 326de9ca.
- 2026-10-26: Doing -> Done. -w writes its literal text and command-line values in the ANSI code page on Windows; -w @file bytes unchanged
