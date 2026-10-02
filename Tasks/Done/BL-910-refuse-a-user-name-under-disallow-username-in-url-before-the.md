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
completed: 2026-10-01
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

- [x] Measured first with `Record-CurlExchange.ps1` or curl directly: a user plus a bad port, a user plus a bad host, and a bad scheme with a user; exit codes and stderr copied into Notes.
- [x] `Curl.Console.UnitTests` pin each measured case under `--disallow-username-in-url`, and without the option the existing exit 3 messages are unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-10-01, local curl 8.21.0 (mingw, Schannel), `curl -sS --disallow-username-in-url <url>`
(and without the option for comparison):

| URL | with the option | without it |
| --- | --- | --- |
| `http://u@127.0.0.1:99999/` | 67 `URL rejected: Credentials was passed in the URL when prohibited` | 3 `Port number was not a decimal number between 0 and 65535` |
| `http://u:p@host:abc/` | 67, same | 3, bad port |
| `http://u@exa%20mple.com/` | 67, same | 3 `Bad hostname` |
| `http://u@[::1]x/` | 67, same | 3, bad port |
| `http://u@:80/`, `http://u@/` | 67, same | 3 `No host part in the URL` |
| `foo://u@host/` | 67, same | 1 `Protocol "foo" not supported` |
| `http://u@ex ample/`, `htt p://u@host/` | 3 `Malformed input to a URL function` | 3, same |
| `http:////u@h/` | 3 `Unsupported number of slashes following scheme` | 3, same |
| `http://u@[::1/` | 3 `bad range specification` (globbing) | 3, same |

Design: curl's parser takes `CURLU_DISALLOW_USER` and returns `CURLUE_USER_NOT_ALLOWED` from
`parse_authority` as soon as it sees a login, so the check moved into the parser rather than
after it. `CurlUrlRejection` gained `UserNotAllowed` (its `curl_url_strerror` text is the
message), `CurlUrlAuthority.Parse` rejects any `@` in the authority when asked, and
`CurlUrl.TryParseDisallowingUser` is the public entry. `CurlCommandRunner.TryParseTransferUrl`
picks it under the option and `UrlRejectedFailure` maps `UserNotAllowed` to exit 67, every other
rejection to exit 3; `ParsedUrlRefusal` keeps only the too-long-host check. The scheme check
stays in the dispatcher, after parsing, which is why `foo://u@host/` now gives 67 as curl does.
No ADR: this matches measured curl, with no choice left open. No option changed, so
`--ai-help` is untouched.

Coverage: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Console`
reports 100/100 and 0 failing members for both.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --disallow-username-in-url refuses a URL with user information with exit 67 before its host, port or scheme is checked, as curl 8.21.0 does
