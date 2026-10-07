---
id: BL-1224
title: Warn about an option argument that starts with a Unicode punctuation character, as curl's OpenSSL build does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1223]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1224 — Warn about an option argument that starts with a Unicode punctuation character, as curl's OpenSSL build does

## Goal

On Linux and macOS, `CommandLineParser` writes `Warning: The argument '<value>' starts with a Unicode character. Maybe ASCII was intended?` for an option value whose first character is in U+2000-U+203F (a smart quote or a dash pasted from a document), as curl 8.21.0's OpenSSL build does; on Windows it writes nothing, as the Schannel build does.

## Context

- Today nothing in `Curl.Cli.UnitLibrary` looks at the first character of an option's value.
- curl 8.21.0, `src/tool_getparam.c` at `curl-8_21_0`: `has_leading_unicode` (lines 2938-2941) is true when the value's bytes start `E2 80` and the third byte has its top bit set, i.e. UTF-8 for U+2000 to U+203F; `getparameter` (lines 3071-3079) checks it for every option that takes a value, after the deprecated-option check and before the value is used, and calls `warnf("The argument '%s' starts with a Unicode character. Maybe ASCII was intended?", nextarg)`. A positional URL is not an option value and is not checked.
- Platform: measured 2026-10-02 on Windows with the mingw Schannel build, `curl -o "–x" file:///nonexist` and `curl -H "“X: y" file:///nonexist` (U+2013, U+201C, passed from PowerShell) print no warning, only `curl: (37) Could not open file /nonexist`: that build reads its arguments in the ANSI code page, so the bytes never start `E2 80`. On Linux and macOS the arguments are UTF-8 and the warning is printed. Use the platform switch the library already has (`CommandLineNumber.LongMaximumFor(bool isWindows)` is the pattern, ADR-0019) so both answers are tested on every platform.
- Warnings go through `CommandLineWarning` like the existing `FileNameLooksLikeFlag`; check whether `-s` hides it the way it hides the other `warnf` warnings and match that.

## Acceptance criteria

- [x] Tests in `Curl.Cli.UnitTests` pin, with the Linux/macOS switch: `-o "–x"`, `-H "“X: y"` and `--data "‘a"` each write the warning with the value as given and still apply the option; a value starting U+1FFF or U+2040, or with the punctuation later in the value, writes none; a URL argument starting U+2013 writes none.
- [x] A test pins that with the Windows switch none of those write a warning.
- [x] `-s` before the option hides or keeps the warning as the other `warnf` warnings in `CommandLineWarning` do, pinned by a test.
- [x] `curl --ai-help` needs no change (no option is added or changed); say so in `Notes`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes on every platform with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Implementation: `CommandLineOption` wraps every value row's applier (all but `NoFunctionValue`, as curl's `getparameter` breaks out for a deprecated option before the check) so it first adds `CommandLineWarning.ArgumentStartsWithUnicode` when `CommandLineOptions.ReadsArgumentsAsUtf8` is set and the value starts U+2000-U+203F. The platform switch is a new `CommandLineParser.Parse(arguments, pathExists, passwordPrompt, dataFileReader, bool isWindows)` overload; the other overloads pass `OperatingSystem.IsWindows()`. Tests: `CommandLineLeadingUnicodeWarningTests` (19).
- The warning goes through `WrappedMessage` (curl's `warnf` wraps at 79 bytes), so `-o "–x"` prints two lines, `...Maybe ASCII was ` and `Warning: intended?`; widths count UTF-8 bytes, so `-H "“X: y"` wraps one word earlier. Pinned in the tests.
- `-s`: it goes through `AddWarningLinesUnlessSilent` like the other `warnf` warnings: `-s` before the option hides it, `-s` after keeps it (curl's `warnf` checks `global->silent` when it is called).
- Choice: values read from a `-K` file are checked too, since curl's `parseconfig` calls the same `getparameter`; a `-h`/`--help` subject is not an option value and is not checked.
- `--ai-help` needs no change: no option is added or changed.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests all green; `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Option values starting U+2000-U+203F warn as curl's OpenSSL build does off Windows; nothing on Windows
