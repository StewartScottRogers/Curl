---
id: BL-1848
title: Close GF-0020 test470: send a config file's UTF-8 bytes unchanged on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1848 — Close GF-0020 test470: send a config file's UTF-8 bytes unchanged on Windows

## Goal

On Windows, a value read from a `-K` config file reaches the wire as the file's own bytes, as curl 8.21.0 sends them, so gap item `behaviour:test470` (GF-0020) measures `match`.

## Context

- Split from BL-1813, which closed the other GF-0020 item (test411).
- Measured 2026-10-08 with curl 8.21.0 (Windows, Schannel) and `Record-CurlExchange.ps1`: a UTF-8 config file holding `-H "X-A: “quoted”"` and `user-agent = “agent”` (U+201C/U+201D). Real curl sends `E2 80 9C ... E2 80 9D` in both header values, prints `Warning: The argument '“agent”' starts with a Unicode character. Maybe ASCII was intended?` (wrapped at 79), exit 0. Curl prints the same warning but sends `93 ... 94`.
- Cause: `ConfigFileSyntax` (Curl.Cli.UnitLibrary) decodes the file as UTF-8 into .NET strings, and on Windows the request side encodes option text in the ANSI code page (`HttpRequestOptions.CommandLineTextEncoding` = `CredentialEncoding.ForPlatform`, set in `Curl.Console/CurlComposition.cs`), which is right for command-line arguments (curl gets them in ANSI) but not for config-file bytes, which curl uses raw.
- Design hint: curl treats a config file's bytes as its `char` strings; the Unicode warning in `has_leading_unicode` checks the UTF-8 bytes. A value from a config file needs to keep its bytes through to the request (and the warning) on Windows; off Windows UTF-8 already round-trips.

## Acceptance criteria

- [ ] A unit test pins that a header and `user-agent` value with U+201C/U+201D read from a config file are sent as their UTF-8 bytes on Windows, with curl's warning.
- [ ] Command-line arguments still go out in the ANSI code page on Windows (existing tests stay green).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
