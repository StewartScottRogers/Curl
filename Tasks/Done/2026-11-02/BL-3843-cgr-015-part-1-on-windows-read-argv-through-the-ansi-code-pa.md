---
id: BL-3843
title: CGR-015 part 1: on Windows, read argv through the ANSI code page with best fit, and echo argv text to stderr in it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Program.cs, Curl.Console/AnsiCodePageArguments.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Cgr015ArgumentTests.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs]
requirement: none
created: 2026-10-26
completed: 2026-11-02
---
# BL-3843 — CGR-015 part 1: on Windows, read argv through the ANSI code page with best fit, and echo argv text to stderr in it
# CGR-015 part 1: on Windows, read argv through the ANSI code page with best fit, and echo argv text to stderr in it

## Goal

On Windows every command-line argument is first converted through the system ANSI code page with best fit, as the reference curl's ANSI argv is, and stderr messages that echo argument text write it in that code page.

## Context

Split from BL-3544 (CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read that task for every repro and the expected bytes). Decision (BL-3544, decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). The existing hooks for that encoding are `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage` (`CommandLineOptions.ConfigFileWireTextEncoding`), `HttpRequestOptions.CommandLineTextEncoding` (set by the engine from `CredentialEncoding.ForPlatform`), and the per-library `SmtpCommandLineText` / `ImapCommandLineText` pattern. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`) rather than reading the host's. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

Add `AnsiCodePageArguments` (Curl.Console) applied in `Program.Main` on Windows only: each argument to the ANSI code page bytes with Windows best fit, then back to a string (so `中` becomes `?`, the full-width digit U+FF10 becomes `0`, `“` stays as cp1252 0x93). Check that the .NET code-page encoding does best fit; if not, build the best-fit table by hand. Expected results then follow from code already there: `-o "中.txt"` becomes `?.txt`, which `WindowsOutputFileNameSanitizer` makes `_.txt`; `http://中文.localhost:20000/` gives exit 3 "URL rejected: No host present"; `http://127.0.0.1:2020＀/a` connects to port 20200. Also write the refusal text that echoes an argument (`curl: option --sílent: is unknown`) in the ANSI code page on Windows (the runner writes stderr as UTF-8 today, `CurlCommandRunner.cs` around lines 1294 and 5221). Record the decision above in a new ADR in `Documentation/Planning/Decisions`.

## Acceptance criteria

- [x] On Windows `curl -s -o "中.txt" http://127.0.0.1:<port>/` creates `_.txt`, `curl -sS "http://中文.localhost:<port>/"` exits 3 with `URL rejected: No host present`, `curl -sS "http://127.0.0.1:2020＀/a"` connects to port 20200, and `curl --sílent <url>` writes the unknown-option line with byte E9, each as real curl does (commands and both results in Notes).
- [x] An ADR records the ANSI-code-page decision, marked "Decided by Claude under Stewart's delegation".
- [x] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [x] Off Windows (UTF-8) the same commands send the same bytes as before.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

- 2026-11-02 (this claim): every criterion already holds on this branch, delivered by BL-3843's split parts, both Done: BL-3892 (`AnsiCodePageArguments` applied in `Program.Main` on Windows, `Cgr015ArgumentTests`, ADR-0838; measured `-o "中.txt"` -> `_.txt`, `http://中文.localhost` -> exit 3 "URL rejected: No host present", `2020０` -> port 20200, identical to the reference ucrt64 curl 8.22.0) and BL-3893 (refused command line echoed in the ANSI code page, `Cgr015StderrEchoTests`, ADR-0840; `--sélent` writes byte E9 as real curl). Their Notes hold the commands, results, fast-test runs and the clean `Measure-CodeQuality.ps1` runs for Curl.Console and Curl.Cli.UnitLibrary. The shift's stash 8258c7e1 is an older copy of the same work, superseded, so nothing was carried over and no code changed in this run.

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
- 2026-10-26: Backlog -> Doing. Criteria at claim: b8bb145c c458aac4 a9b9a7ee f4912012 326de9ca.
- 2026-10-26: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-3843-20261026-063535-L8.jsonl
- 2026-10-26: Backlog -> Deferred. Split into BL-3892 and BL-3893 after hitting its cost cap
- 2026-11-02: Deferred -> Backlog. Stewart 2026-11-02: complete all existing tasks before the third-party handoff.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b8bb145c c458aac4 a9b9a7ee f4912012 326de9ca.
- 2026-11-02: Doing -> Done. ANSI-code-page argv and stderr echo on Windows, delivered by BL-3892 and BL-3893
