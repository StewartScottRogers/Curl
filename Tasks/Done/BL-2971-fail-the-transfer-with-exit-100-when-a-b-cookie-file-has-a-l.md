---
id: BL-2971
title: Fail the transfer with exit 100 when a -b cookie file has a line too long (CookieStore's CURLE_TOO_LARGE)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs, Curl.Engine.UnitTests/CookieEngineTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerCookieFileLineTooLongTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2971 — Fail the transfer with exit 100 when a -b cookie file has a line too long (CookieStore's CURLE_TOO_LARGE)

## Goal

`curl -b <file>` whose file holds a line longer than 4998 characters (before its line feed, a CR counted) sends no request and exits 100 with `curl: (100) A value or data field grew larger than allowed`, as curl 8.22.0 does.

## Context

Measured on curl 8.22.0 (mingw, Schannel) on 2026-10-21 under BL-2916 with `Record-CurlExchange.ps1`: `curl -s -b cf.txt http://127.0.0.1:<port>/` with a 4999-character first line made no request and exited 100; without `-s`, stderr was `curl: (100) A value or data field grew larger than allowed`. The same holds for a CR LF line of 4999 characters with the CR and for a last line of 4999 characters without a line feed. BL-2916 made `CookieStore.LoadCookieFile` and `LoadCookieFileAsync` return `false` in that case (ADR-0739); nothing reads the result yet. Start at `CookieEngine.LoadEveryCookieFileAsync` (`Curl.Engine.UnitLibrary/CookieEngine.cs`), called from `TransferPerformer.LoadCookieFilesAsync` (`TransferPerformer.Setup.cs`) and `CurlCommandRunner` (`Curl.Console/CurlCommandRunner.cs:2251`). Check whether `-b -` (standard input) and a cookie-list `RELOAD` fail the same way before pinning them.

## Acceptance criteria

- [x] A `-b` file with a 4999-character line makes the transfer fail with exit 100 and the message above, sending no request, pinned by a test in `Curl.Engine.UnitTests` or `Curl.Console.UnitTests`.
- [x] A 4998-character line still loads and the transfer goes ahead, pinned by a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Touches widened to the new `Curl.Console.UnitTests/CurlCommandRunnerCookieFileLineTooLongTests.cs`: the runner's new early return needs a Console test for coverage, and no task in Doing on origin/work/dark-factory names it (BL-2954, BL-2967, BL-2975 checked).
- `CookieEngine.LoadCookieFilesAsync` now returns `false` when its first load hit a line too long, stopping at that file as `Curl_cookie_loadfiles` stops at its first error; `TransferPerformer.LoadCookieFilesAsync` turns that into `TransferResult.Failure(CurlExitCode.TooLarge, easy text)`, and `CurlCommandRunner` returns it before alt-svc, HSTS and the request, so curl prints `curl: (100) A value or data field grew larger than allowed`.
- Default taken: `-b -` (standard input) fails the same way, since curl reads it with the same `Curl_cookie_load` code path; not measured separately. A `CURLOPT_COOKIELIST` `RELOAD` and the library handle's `ManagedEasyTransfer.SetUpAsync` still ignore the result (ManagedEasyTransfer.cs is outside touches); the curl tool never sends `RELOAD`.
- Measured: build -warnaserror clean, fast tests green, Measure-CodeQuality on Curl.Engine.UnitLibrary and Curl.Console: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: ba73ee62 4edb6758 8e1ce02b.
- 2026-10-21: Doing -> Done. A -b file with a line over 4998 characters fails the transfer with exit 100 before any request; 4998 still loads
