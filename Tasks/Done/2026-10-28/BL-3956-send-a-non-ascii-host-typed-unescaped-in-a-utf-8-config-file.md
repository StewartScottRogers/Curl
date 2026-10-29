---
id: BL-3956
title: Send a non-ASCII host typed unescaped in a UTF-8 config file as its UTF-8 bytes read as Windows-1252 on Windows (CGR-059)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3963]
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr059ConfigFileHostTests.cs, Curl.Cli.UnitTests/Cgr052Tests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3956 — Send a non-ASCII host typed unescaped in a UTF-8 config file as its UTF-8 bytes read as Windows-1252 on Windows (CGR-059)

## Goal

On Windows, a URL whose host is written unescaped and not ASCII in a UTF-8 `-K` file (`url = "http://ü.example:20200/a"`) gives the same request as curl 8.22.0: `Host: xn--14-ria7423a.example:20200`.

## Context

Found by BL-3953 on 2026-10-28, measured with Record-CurlExchange.ps1 and `--connect-to ::127.0.0.1:20200`. Real Windows curl uses the config file's bytes raw (C3 BC) and libidn2 reads them as Windows-1252 (`Ã¼`), as it does for `%C3%BC`. Typed in an argument, `ü` is the ANSI byte FC and gives `xn--tda` (Curl already matches). Curl reads the config URL as text `ü`, so `CurlUrlHost` cannot tell it from an argument. The fix probably re-spells a config file URL's host as `ConfigFileWireText.Respell` does user information (`WithUserInformationAsWireText` in `CommandLineOptionTable.cs`), once BL-3953 has made `CurlUrlHost` read the host's text as Windows-1252 bytes.

## Acceptance criteria

- [x] On Windows, the config file above run through real curl and Curl gives the same exit code, stdout, stderr and request bytes; the commands and results are recorded in Notes.
- [x] A unit test in `Curl.Cli.UnitTests/Cgr059ConfigFileHostTests.cs` pins it and fails before the fix.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 9069a70d 4e298cd0 779790c0.
- 2026-10-28: Doing -> Done. Config file URL host re-spelled as the file bytes; matches curl 8.22.0 on Windows
