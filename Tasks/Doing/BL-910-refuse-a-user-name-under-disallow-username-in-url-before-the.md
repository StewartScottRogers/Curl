---
id: BL-910
title: Refuse a user name under --disallow-username-in-url before the URL's host and port are validated
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-626]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-910 — Refuse a user name under --disallow-username-in-url before the URL's host and port are validated

## Goal

With `--disallow-username-in-url`, a URL whose user information parses but whose host or port does not fails with exit 67 and `URL rejected: Credentials was passed in the URL when prohibited`, as curl 8.21.0 does, instead of Curl's exit 3 URL-malformed message.

## Context

- Found while delivering BL-626 (see its Notes). curl checks `CURLU_DISALLOW_USER` while parsing the login part of the authority, so any failure later in the authority (host, port) is never reached.
- Measured 2026-09-29, local curl 8.21.0: `curl -sS --disallow-username-in-url http://u@127.0.0.1:99999/` -> exit 67, `curl: (67) URL rejected: Credentials was passed in the URL when prohibited`. Curl today: exit 3, `curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535`.
- `http://u@[::1/` still fails with exit 3 `bad range specification` in curl: globbing runs first.
- Where to start: `CurlUrl.TryParse` / `CurlUrlParser` / `CurlUrlRejection` in `Curl.Protocol.Abstractions.UnitLibrary` (the rejection needs to say whether user information had been parsed), and `CurlCommandRunner.TransferUploadingAsync` / `ParsedUrlRefusal` in `Curl.Console`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` or curl directly: a user plus a bad port, a user plus a bad host, and a bad scheme with a user; exit codes and stderr copied into Notes.
- [ ] `Curl.Console.UnitTests` pin each measured case under `--disallow-username-in-url`, and without the option the existing exit 3 messages are unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
