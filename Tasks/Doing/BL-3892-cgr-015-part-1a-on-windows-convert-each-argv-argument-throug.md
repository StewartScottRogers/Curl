---
id: BL-3892
title: CGR-015 part 1a: on Windows, convert each argv argument through the ANSI code page with best fit, and record the ADR
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Program.cs, Curl.Console/AnsiCodePageArguments.cs, Curl.Console.UnitTests/Cgr015ArgumentTests.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-26
completed:
---
# BL-3892 — CGR-015 part 1a: on Windows, convert each argv argument through the ANSI code page with best fit, and record the ADR

## Goal

On Windows `Program.Main` converts every command-line argument through the system ANSI code page with best fit, as the reference curl's ANSI argv is, and an ADR records the decision.

## Context

Split from BL-3843 (itself split from BL-3544, CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3544 for every repro and the expected bytes). Decision (decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). Existing hooks: `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage`, `HttpRequestOptions.CommandLineTextEncoding`. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`). Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

Add `AnsiCodePageArguments` (Curl.Console) applied in `Program.Main` on Windows only: each argument to the ANSI code page bytes with Windows best fit, then back to a string (`中` becomes `?`, the full-width digit U+FF10 becomes `0`, `“` stays as cp1252 0x93). Check that the .NET code-page encoding does best fit; if not, build the best-fit table by hand. Then `-o "中.txt"` becomes `?.txt`, which `WindowsOutputFileNameSanitizer` makes `_.txt`; `http://中文.localhost:20000/` gives exit 3 "URL rejected: No host present"; `http://127.0.0.1:2020＀/a` connects to port 20200. Record the decision in a new ADR in `Documentation/Planning/Decisions`.

## Acceptance criteria

- [ ] On Windows `curl -s -o "中.txt" http://127.0.0.1:<port>/` creates `_.txt`, `curl -sS "http://中文.localhost:<port>/"` exits 3 with `URL rejected: No host present`, and `curl -sS "http://127.0.0.1:2020＀/a"` connects to port 20200, each as real curl does (commands and results in Notes).
- [ ] An ADR records the ANSI-code-page decision, marked "Decided by Claude under Stewart's delegation".
- [ ] A focused unit test in `Curl.Console.UnitTests/Cgr015ArgumentTests.cs` fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [ ] Off Windows (UTF-8) the same commands send the same bytes as before.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

Split from BL-3843, which was over its cost cap. Continues the stashed work of BL-3843.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: a15a88ba c458aac4 1e0b33a1 f4912012 326de9ca.
