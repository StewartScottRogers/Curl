---
id: BL-3861
title: CGR-016 part 4: Linux and macOS send -b cookie strings and -X methods with non-ASCII UTF-8 as their UTF-8 bytes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3861 — CGR-016 part 4: Linux and macOS send -b cookie strings and -X methods with non-ASCII UTF-8 as their UTF-8 bytes

## Goal

On Linux and macOS, `-b 'c=é'` sends `Cookie: c=` followed by C3 A9 and `-X 'GÉT'` sends the method's UTF-8 bytes, as curl 8.18.0 does, where Curl is believed to send the Latin-1 byte (E9).

## Context

Found by BL-3850 (CGR-016 part 2). The request side writes a `-b` cookie string and a `-X` method one character per byte (Latin-1): the Cookie value goes through `CookieEngine` to `HttpRequestHeadFormatter.AppendAlways` and the method into the request line, both unencoded. `AsRawWireText` in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` re-spells a config file's values as their bytes (CGR-020), and since BL-3850 gives an invalid-byte surrogate as its byte, but leaves a valid non-ASCII command-line character as itself, so `é` goes out as E9. That matches curl on Windows (code page 1252), not on Linux, where curl 8.18.0 sends the argument's UTF-8 bytes. Measured by BL-3850 in WSL with `Record-CurlExchange.ps1 -Curl wsl.exe ... bash -c "curl -s -H $'X-A: \xff' -b $'a=\xff' -b $'c=\xc3\xa9' -X $'G\xffT' http://172.26.96.1:18850/a"`: the request was `47 FF 54 20 2F 61 ...` and `Cookie: a=FF; c=C3 A9`. First confirm what Curl sends today (a Curl.Console or Engine test with a recorded connection) before changing it. The fix is probably: off Windows (`ConfigFileWireTextEncoding is null`), re-spell the command-line value as `Encoding.Latin1.GetString(InvalidUtf8ByteEscape.Encoding.GetBytes(value))`; on Windows, as the ANSI code page's bytes, which for a cp1252 character equals today's Latin-1 output.

## Acceptance criteria

- [ ] `-b 'c=é'` and `-X 'GÉT'` send the same request bytes as curl 8.18.0 on Linux, measured with `Record-CurlExchange.ps1` and recorded in Notes.
- [ ] Every value on Windows goes out as before.
- [ ] A focused unit test in `Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs` pins it and passes on every platform.
- [ ] Curl.Cli.UnitLibrary stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: f55d9e64 5d40e100 1e950046 c285e6d4 779790c0.
