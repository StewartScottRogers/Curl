---
id: BL-204
title: Guess the scheme of a URL given without one
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-204 — Guess the scheme of a URL given without one

## Goal

A URL without a scheme gets `http://` or the scheme its host prefix implies, as curl does.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- curl guesses from host prefixes `ftp.`, `dict.`, `ldap.`, `imap.`, `smtp.`, `pop3.` (https://curl.se/docs/manpage.html#URL).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Each prefix and the default are measured on curl 8.21.0 and pinned in tests.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered as `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs` (`AddGuessedScheme`, `HasScheme`, `GuessScheme`) with 37 tests in `UrlSchemeGuesserTests`. Not wired into `Curl.Console`; that belongs with the URL model work.
- Measured on 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel): `curl -s -m 1 -w '%{url_effective} %{scheme} %{exitcode}
' -o /dev/null "<url>"`. No server was needed: `*.localhost` resolves to loopback, port 1 never answers, and `%{scheme}` / `%{url_effective}` show the guess. Results: `example.localhost:1` -> `http://example.localhost:1/ http`; `ftp.`/`FTP.`/`Ftp.`, `dict.`, `ldap.`, `imap.`, `smtp.`, `pop3.localhost:1` -> that scheme; `pop3s.`, `ftps.`, `ftpx.localhost:1`, `ftp:1`, `[::1]:1`, `a:b@c:1` -> `http`; `ftp.:1` -> `ftp`; `u:p@ftp.localhost:1` -> `ftp`; `ftp.x@dict.localhost:1` -> `dict`; `u@ftp.localhost:1/p@imap.x` -> `ftp`; `imap.localhost/ftp.x` -> `imap`; `HTTP://ftp.localhost:1` and `http:/ftp.localhost:1` -> `http` (named scheme wins); `foo:/x` -> scheme `foo`, `ftp.localhost:/x` -> scheme `ftp.localhost` (both exit 1, unsupported), so a scheme is any `[A-Za-z][A-Za-z0-9+.-]*` followed by `:/`; `host:`, `1host://x`, `ho_st://x`, `a@b@ftp.localhost:1` -> exit 3 (malformed, the URL parser's job, not the guesser's).
- Choice: the guesser only prepends `<scheme>://` and keeps the rest byte for byte; curl's normalisation (the trailing `/` in `url_effective`) belongs to the URL parser. Host = authority (before first `/`, `?`, `#`) after the last `@`. No ADR: this is curl's documented behaviour, not a design decision. `--proto-default` is out of scope.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. UrlSchemeGuesser gives a scheme-less URL the scheme curl 8.21.0 guesses from its host prefix, else http
