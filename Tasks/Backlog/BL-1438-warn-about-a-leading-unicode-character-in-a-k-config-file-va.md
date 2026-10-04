---
id: BL-1438
title: Warn about a leading Unicode character in a -K config file value on Windows as curl does
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-046
created: 2026-10-04
completed:
---
# BL-1438 — Warn about a leading Unicode character in a -K config file value on Windows as curl does

## Goal

On Windows, an option value read from a `-K` config file that starts with a character in U+2000-U+203F (a curly quote, say) gets curl 8.21.0's `Warning: The argument '<value>' starts with a Unicode character. Maybe ASCII was intended?` lines, as it already does off Windows and as real curl does on Windows.

## Context

- Upstream (tag `curl-8_21_0`), `src/tool_getparam.c` lines 2938-2941 and 3076-3079: `getparameter` warns when the value's bytes start `E2 80` followed by a byte with the high bit set - a UTF-8 encoded U+2000-U+203F - for every option value but a deprecated option's. A config file's lines are those raw bytes on every platform; only Windows command-line arguments reach `getparameter` in the ANSI code page, where the test never matches.
- Measured with real curl 8.21.0 (Windows, Schannel, 2026-10-04): a config file holding the line `-H “host:fake”` (UTF-8) read with `--no-progress-meter -K <file> http://127.0.0.1:1/` prints `Warning: The argument '“host:fake”' starts with a Unicode character. Maybe ` and `Warning: ASCII was intended?` (wrapped at 79 columns) before `curl: (7) ...`; the same `-H` value given on the command line prints no warning. Curl prints no warning for the config file case. Upstream test 470 (`Curl.Conformance.UnitTests/UpstreamTestData/test470.rawhttp`) pins the same two lines.
- Curl today: `Curl.Cli.UnitLibrary/CommandLineOption.cs` `WarnAboutLeadingUnicodeThen` warns only when `CommandLineOptions.ReadsArgumentsAsUtf8` is set, which `CommandLineParser.Parse` sets to `!isWindows` for the whole parse, config files included. The fix is to treat values that come from a config file as UTF-8 read on every platform, leaving Windows command-line arguments as they are.

## Acceptance criteria

- [ ] A test in `Curl.Cli.UnitTests` parses, as the Windows build (`isWindows: true`), a config file whose `-H` value starts with U+201C and asserts the two warning lines byte for byte; another asserts the same value given as a command-line argument on the Windows build adds no warning; the existing non-Windows tests still pass.
- [ ] `-s` before `-K` still silences the warning, as it does for command-line values today.
- [ ] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean; `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
