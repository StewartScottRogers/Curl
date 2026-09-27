---
id: BL-216
title: Build Basic and Bearer Authorization values in Curl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-151]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-216 — Build Basic and Bearer Authorization values in Curl.Authentication

## Goal

`Curl.Authentication.UnitLibrary` implements `IHttpAuthenticator` for Basic and Bearer.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item A1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `-u u:p`: `Authorization: Basic dTpw`. `--oauth2-bearer` sends `Authorization: Bearer <token>`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Basic and Bearer values match curl 8.21.0; non-ASCII credential encoding is measured on the mingw reference build and pinned.
- [x] `dotnet build Curl.Authentication.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Authentication`.

## Notes

- Plan item: A1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `BasicAndBearerAuthenticator(Encoding)` implements `IHttpAuthenticator`;
  `CredentialEncoding.ForPlatform(isWindows)` picks the encoding (Curl.Console passes
  `OperatingSystem.IsWindows()` when it wires the authenticator); internal
  `HttpChallengeSchemes.Offered` reads `WWW-Authenticate`/`Proxy-Authenticate` values as
  curl does. Decisions recorded in ADR-0022 (decided by Claude under Stewart's delegation).
- `touches` widened to `Documentation/Planning/Decisions` for ADR-0022 and its README row;
  no task in Doing names it. ADR-0021 is already used on another lane's branch, so this is
  0022.
- Measured 2026-09-26 with curl 8.21.0 (mingw, `/mingw64/bin/curl`), system ANSI code page
  1252, each run as
  `.\Record-CurlExchange.ps1 -Port <p> -CurlArgs <args>,-s,http://127.0.0.1:<p>/ -OutDirectory <dir>`
  from PowerShell, Authorization line read from `request.bin`:
  - `-u u:p` -> `Basic dTpw`; `-u user:` -> `Basic dXNlcjo=`; `-u :p` -> `Basic OnA=`;
    `-u u:p:q` -> `Basic dTpwOnE=`.
  - `-u é:p€` (U+00E9, U+20AC) -> `Basic 6TpwgA==` = bytes `E9 3A 70 80`: Windows-1252.
  - `-u Ω中Ā:p` (U+03A9, U+4E2D, U+0100) -> `Basic Tz9BOnA=` = `O?A:p`: Windows best-fit.
    .NET's `CodePagesEncodingProvider` 1252 encoder gives the same bytes (checked).
  - `--oauth2-bearer tok` -> `Bearer tok`; `--oauth2-bearer té` -> `Bearer t` + byte `E9`.
  - `-u u:p --oauth2-bearer tok` -> `Bearer tok` (only Bearer wanted).
  - With a `401` + `WWW-Authenticate: Basic realm="r"` response and two connections:
    `-u u:p --anyauth` -> first request bare, second `Basic dTpw`;
    `--oauth2-bearer tok --anyauth` with a Bearer challenge -> bare, then `Bearer tok`;
    `-u u:p` -> `Basic dTpw` once, no retry; `-u u:p --digest` -> bare, no retry;
    `-u u:p --basic --digest` -> bare, then `Basic dTpw`.
- Default taken: `NetworkCredential.Domain` is ignored, since `Curl.Cli` never sets it
  (`-u dom\user:p` puts `dom\user` in `UserName`).
- Follow-ups noted on existing tasks rather than filed anew: BL-192 (the parser must map
  `--oauth2-bearer` alone to `AuthSchemes = Bearer`, not `Basic | Bearer`) and BL-237
  (wire `CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())`).
- Gates: `dotnet build -warnaserror` clean; fast tests green (42 in
  Curl.Authentication.UnitTests, all 15 test projects pass); `Measure-CodeQuality.ps1
  -Library Curl.Authentication.UnitLibrary` reports 100% line, 100% branch, 12 members,
  0 failing, worst CRAP 6.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. BasicAndBearerAuthenticator builds curl 8.21.0's Basic and Bearer values, ANSI code page on Windows, UTF-8 elsewhere (ADR-0022)
