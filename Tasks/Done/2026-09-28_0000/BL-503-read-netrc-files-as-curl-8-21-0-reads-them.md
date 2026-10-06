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
completed: 2026-09-28
---
# BL-503 â€” Read .netrc files as curl 8.21.0 reads them

## Goal

A `NetrcFile` reader in `Curl.Authentication.UnitLibrary` returns the login and password for a host (and optional user name) from netrc text exactly as curl 8.21.0's netrc parser picks them: `machine`, `default`, `login`, `password`, `macdef` blocks skipped, quoted tokens with escapes, comments, and the first matching entry winning.

## Context

- Conformance audit 2026-09-28, row 6 (Blocker). Command-line parsing is BL-504, applying it to a transfer is BL-505.
- Upstream: `--netrc` in `Curl.Cli.UnitLibrary/CurlManual.txt` and https://curl.se/docs/manpage.html#-n (curl 8.21.0 is the reference; the web page now shows 8.23.0), and https://everything.curl.dev/usingcurl/netrc.
- The reader takes text (or a `Stream`), not a path, so tests need no disk. Clean-room: build from the documentation and measured behaviour, not by translating `lib/netrc.c`.
- Cases whose answer must be measured through `Record-CurlExchange.ps1` (the `Authorization` header curl sends to a loopback server tells you what it picked): a `default` entry before a `machine` entry, two entries for one host with different logins and `-u user` naming the second, a quoted password with `\"` and `\\`, a `macdef` block, a line with only `machine` and no login, and a file with a syntax error.

## Acceptance criteria

- [x] Measured first: each case above run against the reference curl with `--netrc-file <file> http://<host>:<P>/` via `Record-CurlExchange.ps1`, the request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Authentication.UnitTests` pins every measured case, plus empty text, a comment-only file and a host that matches nothing.
- [x] A syntax error is reported as a typed result the caller can turn into curl's message and exit code (as measured), not an exception.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (2026-09-28)

Reference: Git for Windows `mingw64\bin\curl.exe`, curl 8.21.0 (x86_64-w64-mingw32) Schannel.
Every case: `Record-CurlExchange.ps1 -Port 18503 -CurlArgs -s,-S,--netrc-file,<file>,http://[user@]127.0.0.1:18503/`.
The request is always `GET / HTTP/1.1`, `Host: 127.0.0.1:18503`, [`Authorization: Basic <b64>`],
`User-Agent: curl/8.21.0`, `Accept: */*`; only the Authorization line varies, shown
decoded as `login:password`. stderr is empty and exit 0 unless stated.

| Case (netrc text; `\n` = LF) | Authorization |
| --- | --- |
| `default login d password dp\nmachine 127.0.0.1 login m password mp` | `ZDpkcA==` = `d:dp` (default ahead of the machine wins) |
| two entries `login a password pa` / `login b password pb`, URL user `b@` | `YjpwYg==` = `b:pb` |
| the same, no URL user | `YTpwYQ==` = `a:pa` |
| `machine 127.0.0.1 login q password "a\"b\\c"` | `cTphImJcYw==` = `q:a"b\c` |
| `macdef init\nmachine â€¦ login x password y\n\nmachine â€¦ login m password mp` | `bTptcA==` = `m:mp` |
| `machine 127.0.0.1 password onlypw` | `Om9ubHlwdw==` = `:onlypw` |
| `machine 127.0.0.1` (no login) | none |
| `machine 127.0.0.1 login "abc\n` (unterminated quote) | none; stderr `curl: (26) .netrc error: syntax error`, exit 26 |
| empty file / `# just a comment` / `machine example.com â€¦` / `machine` alone | none |
| `-u b` (no colon) with netrc | curl prompts for the password on the console and hangs; the URL user (`b@`) is what selects an entry, so that is what was measured |

Further measured rules, each pinned by a test in `NetrcFileTests`:
- First qualifying entry wins; `default` qualifies anywhere. Without a URL user an entry
  qualifies with a login or a password (`login a` alone -> `a:`, beats a later full entry;
  `machine h` alone is passed over). With a URL user it needs a password and a login that
  is absent or equal case-sensitively (`login B` vs `b@` -> no match, `b:`).
- Keywords and host names are case-insensitive; unknown tokens (and `account`) are
  ignored and take no value; a value is verbatim (`login password password p` ->
  `password:p`, `login #c` -> `#c`); last `login` wins; a keyword with no value at EOF
  keeps the earlier value.
- `#`-starting token (quoted or not) where a keyword is expected comments out the rest of
  the line; `#` inside a token is kept.
- Quotes: `\n` `\r` `\t` escapes, `\x` -> `x`, may span lines, token ends at the closing
  quote (`"ab"cd` -> `ab`); a quote inside an unquoted token is kept; unterminated or
  `\"` at the end -> syntax error (26).
- `macdef` ends the entry (`login a macdef â€¦` -> `a:`), skips the rest of its line and
  every line to the first empty/whitespace-only line (CRLF too).
- Only space, tab, CR, LF separate tokens (form feed does not). A UTF-8 byte order mark
  glued to `machine` makes it an unknown token.
- Limits, in UTF-8 bytes: token >= 4096 -> 26 (4095 ok, quoted content counted after
  unescaping); line >= 16383 bytes before its LF -> 26 (16382 ok, last line without LF
  the same). A comment or macro line may exceed the token limit. Reading stops at the end
  of the winning entry, so a later error or long token is never seen, but a long line
  before the entry ends (e.g. before EOF) is.

### Decisions (defaults taken)

- `NetrcFile.Find(text, hostName, userName)` takes text; `FindAsync(Stream, â€¦)` decodes
  UTF-8 without stripping a byte order mark, as curl reads bytes. Limits are counted in
  UTF-8 bytes to match curl's byte limits. No ADR: no behaviour choice beyond what was
  measured.
- A syntax error is `NetrcLookupOutcome.SyntaxError` with `ExitCode` 26 and
  `NetrcLookupResult.SyntaxErrorMessage`; `Login`/`Password` null mean the entry has none
  (curl then uses the URL user / an empty password), left to BL-505.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. NetrcFile finds the login and password curl 8.21.0 picks from netrc text, with syntax errors as a typed exit-26 result
