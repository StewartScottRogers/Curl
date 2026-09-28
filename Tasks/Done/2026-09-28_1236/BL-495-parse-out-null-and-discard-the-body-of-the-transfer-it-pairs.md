---
id: BL-495
title: Parse --out-null and discard the body of the transfer it pairs with
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-495 — Parse --out-null and discard the body of the transfer it pairs with

## Goal

`--out-null` takes an output slot like `-o` and discards that URL's body, so `curl --out-null URL -w '%{http_code}'` writes only the write-out, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 11 (Blocker, S).
- Alias-table row `out-null` (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`); no row in `CommandLineOptionTable.cs`.
- Output options pair with URLs as curl's tool pairs them (ADR-0029); `UrlOutput.cs` in Cli and `OutputFileTarget.cs` in Console are where a discarding target fits.
- `%{filename_effective}`, `-D`, `-i` and the progress meter behaviour with `--out-null` must be measured, not assumed.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--out-null` with one URL, with two URLs (one `--out-null`, one `-o`), with `-i`, and with `-w '%{filename_effective}|%{size_download}'`; stdout, stderr and exit code copied into Notes.
- [x] `--out-null` parses and pairs with URLs in command-line order as ADR-0029 pairs `-o`; `Curl.Cli.UnitTests` covers the pairing.
- [x] `Curl.Console.UnitTests` tests pin the measured stdout bytes and `-w` values; no file is created.
- [x] New tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

Measured 2026-09-28 with curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1`, the
server answering `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: b\r\n\r\nhello`
(`u` = `http://127.0.0.1:48495/a`, `u2` = `.../b`):

| Arguments | Exit | stdout | stderr |
| --- | --- | --- | --- |
| `-s --out-null u -w %{http_code}` | 0 | `200` | empty |
| `-s --out-null -o b.txt u u2` | 0 | empty; `b.txt` holds `hello` | empty |
| `-s -o c.txt --out-null u u2` | 0 | empty; `c.txt` holds `hello` | empty |
| `-s -i --out-null u` | 0 | empty | empty |
| `-s --out-null u -w %{filename_effective}\|%{size_download}` | 0 | `\|5` | empty |
| `-s -D - --out-null u` | 0 | `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nX-A: b\r\n\r\n` | empty |
| `-s u --out-null` | 0 | empty | empty |
| `-s --no-out-null u` | 0 | empty | empty |
| `-s --remote-name-all --out-null u` | 0 | empty; no file | empty |
| `-s --out-null u -w "%{http_code}\n"` | 0 | `200\n` (LF, binary mode) | empty |
| `-s --out-null u u2 -w "%{urlnum}\n"` | 0 | `0\nhello1\n` | empty |
| `--out-null u` | 0 | empty | the progress meter, as for a file |
| `--out-null u --out-null -w %{http_code}` | 0 | `200` | meter, then `Warning: Got more output options than URLs` |

Decisions (sensible defaults, no ADR needed - each follows the measurement):
- `--out-null` is a `NegatableFlag` whose `--no-` spelling does the same thing, because curl
  8.21.0 discards under `--no-out-null` too (its handler ignores the toggle).
- It pairs as one more output option (`UrlOutput.DiscardsBody`, `CommandLineOptions.PairDiscardedBody`),
  clearing `UsesRemoteName` so `--remote-name-all` saves nothing.
- The runner sends a discarded body to `Stream.Null` with the progress meter treated as for a
  file, and counts the transfer as switching standard output to binary (the measured LF). The
  private members that decide binary mode were renamed from "writes to standard output" to
  "switches standard output to binary", which is what they now tell.
- `OutputFileTarget.cs` was not touched: a discarding target has no path, so it lives in
  `UrlTransfer.DiscardsBody` instead.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --out-null (and --no-out-null) pairs with URLs like -o and discards that URL's body and -i headers; -w, -D and the progress meter behave as curl 8.21.0
