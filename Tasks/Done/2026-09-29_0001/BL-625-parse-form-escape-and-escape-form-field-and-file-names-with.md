---
id: BL-625
title: Parse --form-escape and escape form field and file names with backslashes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-625 — Parse --form-escape and escape form field and file names with backslashes

## Goal

With `--form-escape`, `-F` part names and file names in `Content-Disposition` are escaped with backslashes (`"` as `\"`, `\` as `\\`) instead of curl's default percent-encoding, byte for byte as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 21 (Major, S).
- Multipart bodies are built in `Curl.Core.UnitLibrary/Multipart/` (ADR-0027 records libcurl's escaping); `Curl.Console/MultipartFormPartMapping.cs` maps the options.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `-F 'a"b=1'`, `-F 'f=@dir/q"x.txt'` and a name with a backslash, with and without `--form-escape`; `request.bin` copied into Notes.
- [x] `Curl.Core.UnitTests` pin the `Content-Disposition` bytes for each case; `Curl.Cli.UnitTests` pin parsing.
- [x] Tests are platform-neutral (no drive-letter file names).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-29 with the local curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1`,
`-sS http://127.0.0.1:<port>/`. The `Content-Disposition` line of each `request.bin` (`<CR>` is byte 13,
`<LF>` byte 10; every line ends CR LF):

| `-F` value | default | `--form-escape` |
| --- | --- | --- |
| `a"b=1` | `form-data; name="a%22b"` | `form-data; name="a\"b"` |
| `a\b=1` | `form-data; name="a\b"` | `form-data; name="a\\b"` |
| `c<CR>d<LF>e=1` | `form-data; name="c%0Dd%0Ae"` | `form-data; name="c<CR>d<LF>e"` |
| `f=@<dir>\x.txt;filename="q\"x.txt"` | `name="f"; filename="q%22x.txt"` | `name="f"; filename="q\"x.txt"` |
| `f=@<dir>\x.txt;filename=q"x.txt` | `name="f"; filename="q%22x.txt"` | `name="f"; filename="q\"x.txt"` |
| `f=@<dir>\x.txt;filename="q\\x.txt"` | `name="f"; filename="q\x.txt"` | `name="f"; filename="q\\x.txt"` |

Windows cannot name a file `q"x.txt`, so the quoted file name was given with `;filename=`; the
builder test for a quote in the path's own base name uses the in-memory file system. With a
relative `dir/x.txt` the recorder's working directory is not the file's, so curl exited 26; the
absolute path was used instead. `--form-escape --no-form-escape` sends `a%22b`, `--no-form-escape
--form-escape` sends `a\"b`: the last spelling wins. The option is per `--next` group, as libcurl's
`mime_options` is kept in each `OperationConfig`.

This matches libcurl's `escape_string`: `--form-escape` (`CURLMIMEOPT_FORMESCAPE`) swaps the WHATWG
table (`"` `%22`, CR `%0D`, LF `%0A`) for the MIME table (`\` `\\`, `"` `\"`), so CR and LF go raw.

Choices (defaults taken, no ADR needed - behaviour is pinned to measured curl): `MultipartNameEscaping`
(`Percent`, `Backslash`) in `Curl.Core.UnitLibrary/Multipart`, passed to a new
`MultipartFormBodyBuilder.BuildAsync(parts, nameEscaping, token)` overload rather than the
constructor, because `Curl.Console` shares one builder across every transfer and option group.
`MultipartFormPartMapping.NameEscapingOf` picks it from `CommandLineOptions.FormEscape`, which keeps
`CurlCommandRunner.TransferUploadingAsync` at complexity 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --form-escape escapes -F names and file names with backslashes as curl 8.21.0 does
