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
completed: 2026-10-26
---
# BL-3892 — CGR-015 part 1a: on Windows, convert each argv argument through the ANSI code page with best fit, and record the ADR

## Goal

On Windows `Program.Main` converts every command-line argument through the system ANSI code page with best fit, as the reference curl's ANSI argv is, and an ADR records the decision.

## Context

Split from BL-3843 (itself split from BL-3544, CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3544 for every repro and the expected bytes). Decision (decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). Existing hooks: `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage`, `HttpRequestOptions.CommandLineTextEncoding`. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`). Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

Add `AnsiCodePageArguments` (Curl.Console) applied in `Program.Main` on Windows only: each argument to the ANSI code page bytes with Windows best fit, then back to a string (`中` becomes `?`, the full-width digit U+FF10 becomes `0`, `“` stays as cp1252 0x93). Check that the .NET code-page encoding does best fit; if not, build the best-fit table by hand. Then `-o "中.txt"` becomes `?.txt`, which `WindowsOutputFileNameSanitizer` makes `_.txt`; `http://中文.localhost:20000/` gives exit 3 "URL rejected: No host present"; `http://127.0.0.1:2020＀/a` connects to port 20200. Record the decision in a new ADR in `Documentation/Planning/Decisions`.

## Acceptance criteria

- [x] On Windows `curl -s -o "中.txt" http://127.0.0.1:<port>/` creates `_.txt`, `curl -sS "http://中文.localhost:<port>/"` exits 3 with `URL rejected: No host present`, and `curl -sS "http://127.0.0.1:2020＀/a"` connects to port 20200, each as real curl does (commands and results in Notes).
- [x] An ADR records the ANSI-code-page decision, marked "Decided by Claude under Stewart's delegation".
- [x] A focused unit test in `Curl.Console.UnitTests/Cgr015ArgumentTests.cs` fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [x] Off Windows (UTF-8) the same commands send the same bytes as before.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

Split from BL-3843, which was over its cost cap. Continues the stashed work of BL-3843.

- Carried over from stash 5c613a00: `AnsiCodePageArguments`, its four argument tests and the ADR. The
  stash's standard-error half (`CommandLineRefusal.EchoesConfigFileText`, `CurlCommandRunner`'s
  ANSI refusal lines and their three tests) is BL-3893's and was left to it.
- `Program.Main` now applies it after `RawCommandLineArguments.ForCurrentPlatform` (added since the
  stash), so the Linux and macOS raw-argv path is unchanged and Windows goes through the code page.
  It takes and returns `IReadOnlyList<string>` to fit that.
- .NET's code-page encodings do Windows best fit: `Cgr015ArgumentTests` shows `中` to `?` and U+FF10
  to `0` under cp1252, so no hand-built table was needed. The tests fail before the change (the type
  did not exist) and pass after it.
- The ADR was numbered 0836 (0820 to 0835 were taken since the stash).
- Measured 2026-10-26 with `Record-CurlExchange.ps1`, reference ucrt64 curl 8.22.0, then `-Curl`
  this build's `curl.exe`; identical results:
  - `-s -o "<dir>\中.txt" http://127.0.0.1:18843/`: exit 0, creates `_.txt` (both).
  - `-sS "http://中文.localhost:18843/"`: exit 3, `curl: (3) URL rejected: No host present`, stderr
    bytes identical (both).
  - `-sS "http://127.0.0.1:2020０/a"` (server on 20200): exit 0, `GET /a HTTP/1.1`,
    `Host: 127.0.0.1:20200`, request bytes identical (both).
- Off Windows `ForPlatform` returns the same list instance (pinned by
  `ForPlatform_OffWindows_ReturnsTheArgumentsUnchanged`), so the bytes sent are as before; the
  tests inject cp1252, so they are platform-neutral.
- `dotnet build` clean, fast tests green (46 projects, 0 failed), `Measure-CodeQuality.ps1 -Library
  Curl.Console`: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: a15a88ba c458aac4 1e0b33a1 f4912012 326de9ca.
- 2026-10-26: Doing -> Done. Windows argv now read through the ANSI code page with best fit; matches reference curl on all three commands
