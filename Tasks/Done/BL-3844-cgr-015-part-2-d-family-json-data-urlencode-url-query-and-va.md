---
id: BL-3844
title: CGR-015 part 2: -d family, --json, --data-urlencode, --url-query and --variable encode command-line text in the ANSI code page on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr015Tests.cs, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs, Curl.Cli.UnitTests/CommandLineProtocolOptionTests.cs, Curl.Cli.UnitTests/CommandLinePostDataOptionTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3844 — CGR-015 part 2: -d family, --json, --data-urlencode, --url-query and --variable encode command-line text in the ANSI code page on Windows
# CGR-015 part 2: -d family, --json, --data-urlencode, --url-query and --variable encode command-line text in the ANSI code page on Windows

## Goal

The `-d`, `--data`, `--data-ascii`, `--data-binary`, `--data-raw`, `--json`, `--data-urlencode`, `--url-query` and `--variable` options turn command-line text into bytes in the ANSI code page on Windows, so `-d é` sends E9 with Content-Length 1.

## Context

Split from BL-3544 (CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read that task for every repro and the expected bytes). Decision (BL-3544, decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). The existing hooks for that encoding are `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage` (`CommandLineOptions.ConfigFileWireTextEncoding`), `HttpRequestOptions.CommandLineTextEncoding` (set by the engine from `CredentialEncoding.ForPlatform`), and the per-library `SmtpCommandLineText` / `ImapCommandLineText` pattern. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`) rather than reading the host's. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

Today they use `Encoding.UTF8.GetBytes` (`CommandLineOptions.AppendPostData` around line 2377, `CommandLineOptionTable.cs` around lines 836, 972 and 979). Use the encoding `ConfigFileWireTextEncoding` already holds on Windows (UTF-8 elsewhere), for text from the command line only: text read from a file (`@file`) stays as its bytes, and text from a UTF-8 config file keeps the behaviour `ReadingConfigFileAsWireText` gives it. MQTT payloads come from the same bytes, so `-d héllo mqtt://...` then sends `68 E9 6C 6C 6F`.

## Acceptance criteria

- [x] With cp1252: `-d é` posts E9 (Content-Length 1), `--data-urlencode é` posts `%E9`, `--data-urlencode x=é` posts `x=%E9`, `--url-query q=é` adds `?q=%E9`, and `-d €` gives byte 80, as real curl on Windows does (commands and both results in Notes).
- [x] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [x] Off Windows (UTF-8) the same commands send the same bytes as before.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

- Real curl (Git for Windows ucrt64 curl, `Record-CurlExchange.ps1`, cp1252 host), 2026-10-26: `-d é` -> body `E9`, Content-Length 1; `--data-urlencode é` -> `%E9`; `--data-urlencode x=é` -> `x=%E9`; `--url-query q=é` -> `GET /a?q=%E9`; `-d €` -> `80`; `--json é` -> `E9`. Curl before the change: `C3 A9`, `%C3%A9`, `x=%C3%A9`, `q=%C3%A9`, `E2 82 AC`; after it, the curl bytes on Windows and the UTF-8 ones off Windows.
- Change: `CommandLineOptions.CommandLineTextBytes` encodes a typed value (or a line of a config file read in the wire encoding, which gives back the file's bytes) in `ConfigFileWireTextEncoding` when set, else UTF-8; a UTF-8 config file's line stays UTF-8 (its own bytes). `-d`-family, `--json`, `--data-urlencode` and `--url-query` contents use it; `@file` contents stay the file's bytes. The MQTT payload comes from `PostData`, so it follows.
- Decision (sensible default): an `--expand-` value whose references were replaced stays UTF-8 (`ApplyingExpandedValue`, set in `CommandLineParser.ApplyExpandedValue`), because the variable layer stores and expands bytes as UTF-8; encoding its decoded string in cp1252 would break a variable read from a UTF-8 file. Making `--variable` carry ANSI text is filed as BL-3845 (renumbered by the shift if it collides). The decision on the encoding itself is BL-3544's.
- Touches widened, no other Doing task names them: `CommandLineParser.cs` and `CommandLineGlobalState.cs` (the expanded-value flag), and `CommandLineProtocolOptionTests.cs` / `CommandLinePostDataOptionTests.cs`, whose two tests pinned UTF-8 through the host-dependent parse and failed on Windows after the change (the fail-before/pass-after evidence: `Cgr015Tests` asserts the single ANSI byte where the old code gave `C3 A9`); they now run off Windows only, `Cgr015Tests` pins the Windows answer.
- Tests are platform-neutral: the Windows parse uses the host ANSI code page, which `ConfigFileWireText.WindowsAnsiCodePage` falls back to Windows-1252 for on Linux and macOS (no ANSI code page there); `AnsiBytes` asserts the code page gives one byte. CI criterion met by keeping the change platform-neutral and the fast tests green here.
- Verified 2026-10-26: `dotnet build` clean; fast tests: 46 test runs passed, 0 failed; `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
- 2026-10-26: Backlog -> Doing. Criteria at claim: c9d5bfe4 a9b9a7ee f4912012 326de9ca.
- 2026-10-26: Doing -> Done. -d family, --json, --data-urlencode and --url-query send command-line text in the ANSI code page on Windows (-d é posts E9), UTF-8 elsewhere
