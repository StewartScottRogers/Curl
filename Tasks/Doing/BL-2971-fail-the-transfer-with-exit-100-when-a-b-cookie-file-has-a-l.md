---
id: BL-2971
title: Fail the transfer with exit 100 when a -b cookie file has a line too long (CookieStore's CURLE_TOO_LARGE)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs, Curl.Engine.UnitTests/CookieEngineTests.cs, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2971 — Fail the transfer with exit 100 when a -b cookie file has a line too long (CookieStore's CURLE_TOO_LARGE)

## Goal

`curl -b <file>` whose file holds a line longer than 4998 characters (before its line feed, a CR counted) sends no request and exits 100 with `curl: (100) A value or data field grew larger than allowed`, as curl 8.22.0 does.

## Context

Measured on curl 8.22.0 (mingw, Schannel) on 2026-10-21 under BL-2916 with `Record-CurlExchange.ps1`: `curl -s -b cf.txt http://127.0.0.1:<port>/` with a 4999-character first line made no request and exited 100; without `-s`, stderr was `curl: (100) A value or data field grew larger than allowed`. The same holds for a CR LF line of 4999 characters with the CR and for a last line of 4999 characters without a line feed. BL-2916 made `CookieStore.LoadCookieFile` and `LoadCookieFileAsync` return `false` in that case (ADR-0739); nothing reads the result yet. Start at `CookieEngine.LoadEveryCookieFileAsync` (`Curl.Engine.UnitLibrary/CookieEngine.cs`), called from `TransferPerformer.LoadCookieFilesAsync` (`TransferPerformer.Setup.cs`) and `CurlCommandRunner` (`Curl.Console/CurlCommandRunner.cs:2251`). Check whether `-b -` (standard input) and a cookie-list `RELOAD` fail the same way before pinning them.

## Acceptance criteria

- [ ] A `-b` file with a 4999-character line makes the transfer fail with exit 100 and the message above, sending no request, pinned by a test in `Curl.Engine.UnitTests` or `Curl.Console.UnitTests`.
- [ ] A 4998-character line still loads and the transfer goes ahead, pinned by a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: ba73ee62 4edb6758 8e1ce02b.
