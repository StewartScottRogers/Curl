---
id: BL-503
title: Read .netrc files as curl 8.21.0 reads them
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-503 — Read .netrc files as curl 8.21.0 reads them

## Goal

A `NetrcFile` reader in `Curl.Authentication.UnitLibrary` returns the login and password for a host (and optional user name) from netrc text exactly as curl 8.21.0's netrc parser picks them: `machine`, `default`, `login`, `password`, `macdef` blocks skipped, quoted tokens with escapes, comments, and the first matching entry winning.

## Context

- Conformance audit 2026-09-28, row 6 (Blocker). Command-line parsing is BL-504, applying it to a transfer is BL-505.
- Upstream: `--netrc` in `Curl.Cli.UnitLibrary/CurlManual.txt` and https://curl.se/docs/manpage.html#-n (curl 8.21.0 is the reference; the web page now shows 8.23.0), and https://everything.curl.dev/usingcurl/netrc.
- The reader takes text (or a `Stream`), not a path, so tests need no disk. Clean-room: build from the documentation and measured behaviour, not by translating `lib/netrc.c`.
- Cases whose answer must be measured through `Record-CurlExchange.ps1` (the `Authorization` header curl sends to a loopback server tells you what it picked): a `default` entry before a `machine` entry, two entries for one host with different logins and `-u user` naming the second, a quoted password with `\"` and `\\`, a `macdef` block, a line with only `machine` and no login, and a file with a syntax error.

## Acceptance criteria

- [ ] Measured first: each case above run against the reference curl with `--netrc-file <file> http://<host>:<P>/` via `Record-CurlExchange.ps1`, the request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Authentication.UnitTests` pins every measured case, plus empty text, a comment-only file and a host that matches nothing.
- [ ] A syntax error is reported as a typed result the caller can turn into curl's message and exit code (as measured), not an exception.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
