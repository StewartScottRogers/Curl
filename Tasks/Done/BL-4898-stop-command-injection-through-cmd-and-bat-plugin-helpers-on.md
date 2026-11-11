---
id: BL-4898
title: Stop command injection through .cmd and .bat plugin helpers on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs, Curl.Extended.Protocol.Plugin.UnitLibrary/BatchHelperCommandLine.cs, Curl.Extended.Protocol.Plugin.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.Plugin.UnitTests/BatchHelperArgumentTests.cs, Curl.Extended.Protocol.Plugin.UnitTests/BatchHelperCommandLineTests.cs, Documentation/Conformance/Extended/Plugin.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4898 — Stop command injection through .cmd and .bat plugin helpers on Windows

## Goal

A URL handed to a `curl-proto-<scheme>.cmd` or `.bat` helper can never run another command: the helper gets exactly one argument byte-equal to the URL, or the transfer is refused with a clear error.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-001 (Critical, confirmed: reproduced at least twice).

- Repro: Put `curl-proto-demo.cmd` first on PATH (it reads a line, echoes `get`, a blank line, reads a line, echoes `ok` and `body`). In an empty directory run `curl.exe -sS "demo://host/a&echo.>pwn1.txt"`, `curl.exe -sS "demo://host/a\"&echo.>pwn2.txt&\""` and `curl.exe -sS "demo://host/%PATH%"`.
- Expected: Plugin.md Invocation-1: "The program runs with the URL as typed as its one argument"; Discovery-3 supports `.BAT;.CMD` through PATHEXT. No other command runs.
- Actual: `pwn1.txt` and `pwn2.txt` are created: cmd.exe runs `echo.>...` as a separate command. Case 3 passes the expanded PATH to the helper split into about 60 arguments. This is the "BatBadBut" class (CVE-2024-24576): anyone who can supply a URL can run commands on a machine with a batch-file helper.
- Where to start: `SystemHelperProcessStarter.Start()`: `new ProcessStartInfo(program)` plus `ArgumentList.Add(url)`; .NET launches `.cmd`/`.bat` through `cmd.exe /c` without escaping `& | < > ^ " %`. Options: refuse batch helpers, launch them through an explicit `cmd.exe /d /s /c` with a fully cmd-escaped argument (and `%` neutralised), or refuse URLs holding cmd metacharacters for batch helpers. Record the choice in an ADR and update Plugin.md Discovery-3.

## Acceptance criteria

- [x] None of the three repro commands creates a `pwn*.txt` file; the helper either receives exactly one argument byte-equal to the URL (with `%PATH%` literal) or the transfer is refused with a clear error and a non-zero exit.
- [x] A regression test in `Curl.Extended.Protocol.Plugin.UnitTests` covers `&`, `"` and `%` in a URL bound for a batch helper.
- [x] `Documentation/Conformance/Extended/Plugin.md` Discovery-3 states how batch helpers are handled.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Decision (ADR-1238): on Windows a `.bat`/`.cmd` helper is started as `cmd.exe /e:ON /v:OFF /d /c ""<program>" "<url>""`, each value quoted and every `%` written `%%cd:~,%` (Rust's fix for CVE-2024-24576). A program or URL holding `"`, CR, LF or NUL, which cmd cannot carry, is refused before the helper starts: `curl: (7) Failed to run curl-proto-demo.cmd: a batch helper cannot receive a double quote, carriage return, line feed or NUL`. Refusing batch helpers or every metacharacter was rejected because `&` and `%` are in most real URLs.
- The command line is built by a new `BatchHelperCommandLine` (its own file, so it is unit tested and covered while the `Process` adapter stays excluded per ADR-0083). The tests are in `BatchHelperCommandLineTests.cs`, not `BatchHelperArgumentTests.cs`, to follow the `<ClassName>Tests` rule. Both files, and the library's CLAUDE.md, are added to touches; no other task in Doing names them.
- Measured with the built curl.exe and a `curl-proto-demo.cmd` on PATH: `demo://host/a&echo.>pwn1.txt|x<y^z` reaches the helper byte-equal through `%~1` and runs no other command; `demo://host/a"&echo.>pwn2.txt&"` is refused with exit 7 and creates no file; `demo://host/%PATH%/x%20y^!z%` arrives literal as one argument.
- Platform: the tests are pure string building with forward-slash paths, so they are platform-neutral; the cmd.exe path is taken only when `OperatingSystem.IsWindows()`.
- Environment: global.json pins SDK 10.0.401, which is not installed on this host (only 10.0.100-rc.1 and 11.0.x are), so `dotnet build` failed at SDK resolution. The build, fast tests, format and quality run were done with global.json moved aside for each command and put back (SDK 11.0.100, net10.0 target). The build is clean. Of 113 fast test assemblies, 112 pass; `Curl.Original.Protocol.Ldap.UnitTests` fails one test (`Start_NtlmWithNoProtection_FirstTokenAsksNeitherToSignNorSeal`: on this host SSPI sets the NTLM sign flag). That is unrelated to this change and filed as BL-4951. Measure-CodeQuality for Curl.Extended.Protocol.Plugin.UnitLibrary: 100% line, 100% branch, 0 failing members.

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 8f505c9f ff2b8363 fab8595d a0b49a29.
- 2026-11-11: Doing -> Done. Batch helpers run through cmd.exe with the URL quoted and % neutralised; URLs cmd cannot carry are refused (ADR-1238)
