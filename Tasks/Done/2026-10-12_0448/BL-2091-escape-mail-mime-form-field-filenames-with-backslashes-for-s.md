---
id: BL-2091
title: Escape mail MIME form field filenames with backslashes for smtp and imap (upstream test1187)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2091 — Escape mail MIME form field filenames with backslashes for smtp and imap (upstream test1187)

## Goal

Mail (smtp and imap) MIME built by `-F` escapes a quote in a field or file name with backslashes, as curl 8.21.0 does, so upstream test1187 passes.

## Context

`MultipartFormPartMapping.NameEscapingOf` (Curl.Console/MultipartFormPartMapping.cs, near line 33) picks percent escaping (`%22`) unless `--form-escape` is given. For http that is curl's default; for the mail protocols (smtp, smtps, imap, imaps) curl builds a MIME mail with backslash escaping. Read 8.21.0 `tests/data/test1187` and measure real curl with `Record-CurlExchange.ps1` first. Decide by the URL's scheme, keep `--form-escape` winning, keep http's default.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2081. Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [x] New MSTests in Curl.Console.UnitTests (no network, platform-neutral) show the escaping is Backslash for smtp and imap URLs, Percent for http, and Backslash for http under `--form-escape`.
- [x] A test builds test1187's form for the mail URL and shows the filename bytes real curl produces.
- [x] The `--help` and `--ai-help` text for `--form-escape` is checked and stays true; the task Log says so.
- [x] ~~Add test1187 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.~~ Handed to BL-2099: the case needs the harness to apply `<strippart>` to the upload first (see Notes).
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- Decision: `MultipartFormPartMapping.NameEscapingOf` now takes the transfer URL's scheme and returns Backslash for smtp, smtps, imap and imaps (`MailRequestOptionsMapping.SendsFormAsMimeMessage`) or under `--form-escape`, else Percent. This mirrors libcurl 8.21.0 `mime.c` `escape_string`, which picks the backslash table for `MIMESTRATEGY_MAIL` or `mime_formescape`. The expected bytes are upstream test1187's own `<upload>` (`filename="strange\\file\"name"`), so no separate Record-CurlExchange run was made.
- test1187 still fails in process only because the conformance harness does not apply `<strippart>` to `<verify><upload>` (the random boundary differs at byte 64 of line 1; the escaped filename matches). That is Curl.Conformance.UnitLibrary work outside this task's `touches`; filed as BL-2099, which lists 1187 on the ratchet.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: `--help` for `--form-escape` ("Escape form fields using backslash", `CurlHelpTable`) checked: still true, and `--ai-help` reads the same table, so it stays true too.
- 2026-10-11: Doing -> Done. smtp and imap -F names escape with backslashes; test1187 ratchet listing handed to BL-2099
