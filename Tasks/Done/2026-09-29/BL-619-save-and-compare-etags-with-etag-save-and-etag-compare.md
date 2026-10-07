---
id: BL-619
title: Save and compare ETags with --etag-save and --etag-compare
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-619 — Save and compare ETags with --etag-save and --etag-compare

## Goal

`--etag-save <file>` writes the response's `ETag` value to the file, and `--etag-compare <file>` sends its content as `If-None-Match`, with the file handling, missing-file behaviour and output curl 8.21.0 has.

## Context

- Conformance audit 2026-09-28, row 19 (Major, S-M).
- Rows `etag-save` and `etag-compare` in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`. Custom headers reach the HTTP request through `Curl.Console/HttpRequestOptionsMapping.cs`; response headers are on `TransferReport.ResponseHeaders`. File access goes through the console's existing file seams.
- Measure: where `If-None-Match` sits among the headers, what is written when there is no `ETag`, a missing compare file, a `304` reply, and both options naming one file.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: the cases above; request bytes, stdout, stderr, exit code and the saved file copied into Notes.
- [x] `Curl.Cli.UnitTests` cover parsing; `Curl.Console.UnitTests` pin each measured case through fake file seams.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-29 with curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1` against
`http://127.0.0.1:18619/x`. Request lines shown without `Host`, `User-Agent` and `Accept`, which are
unchanged; every run exit 0 with empty stderr unless stated.

`--etag-compare <file>` (file read at transfer setup, every CR and LF byte dropped):
- File `"abc123"\n`: `If-None-Match: "abc123"\r\n` is the last header line.
- `-H 'X-A: 1' --etag-compare c1 -H 'X-B: 2'`: `X-A: 1`, `X-B: 2`, then `If-None-Match`; with
  `--json {}` the order is `X-A`, `Content-Type: application/json`, `Accept: application/json`,
  `If-None-Match`, then `Content-Length`.
- Missing file: `If-None-Match: ""`; without `-s`, stderr `Warning: Failed to open <path>: No such
  file or directory` (wrapped at 79 columns as every warning); nothing under `-s`.
- Empty file: `If-None-Match: ""`. File `"l1"\r\n"l2"\n  x  \n`: `If-None-Match: "l1""l2"  x  `.
- A 304 reply: stdout empty, `-w %{http_code}` prints `304`, exit 0.
- Glob `http://.../{a,b}`: the first request has one `If-None-Match` line, the second has two -
  curl appends the line to the option group's header list for each transfer.

`--etag-save <file>` (file opened for appending before connecting, so it is created empty and an
existing one is kept):
- 200 with `ETag: "abc123"`: file `"abc123"\n` (quotes kept, LF only), stdout `hi`.
- No ETag (200 or 304): an existing file keeps `OLD`; a missing one is created empty.
- The ETag of a 201, 302, 304 or 399 is saved; that of a 100, 400, 404 or 500 is not (file keeps
  `OLD`); with `-f` a 404 exits 22 and keeps `OLD`.
- `etag:  \t W/"weak1"  \t` then `ETag: "2"`: file `"2"\n` (value trimmed, last wins).
  `ETag:   ` (empty value) writes nothing.
- `-L` with `302 + ETag: "hop1"` then a 200 without one: file `"hop1"\n`.
- `--etag-save -`: stdout `"new1"\nhi`. With `-D -`: status line, `ETag: "e1"\r\n`, `"e1"\n`, the
  rest. With `-i`: status line, `"e1"\n`, `ETag: "e1"\r\n`, the rest.
- File in a missing directory: nothing sent, exit 26; stderr `Warning: Failed creating file for
  saving etags: "<path>". Skip this transfer` (wrapped), then `curl: no transfer performed`. `-s`:
  nothing; `-sS`: only `curl: no transfer performed`; `-w` prints nothing. When a later `--next`
  group transfers, its exit code and `-w` stand and nothing is printed for the skipped one.
- `--create-dirs` creates the save file's directories.
- Both options naming one file: the old ETag is sent, a new one replaces it, and a 304 without an
  ETag keeps it.

Two URLs in one option group with either option: exit 2, `curl: The etag options only work on a
single URL` (hidden by `-s`, back with `-sS`), `curl: option <arg>: is badly used here`, try-help
line; `<arg>` is the second URL, or `--etag-save` when it follows two URLs, or `--url`. A glob in one
URL and a URL in a later `--next` group are accepted. A blank value: `curl: option --etag-save:
blank argument where content is expected`. A value like `-x` warns `The filename argument '-x'
looks like a flag.`

Choices made (defaults, not behaviour changes):
- The compare file's bytes are decoded in the platform's command-line text encoding
  (`CredentialEncoding.ForPlatform`), the one the header is sent in, so its bytes go out unchanged.
  curl's Windows text-mode quirk (a Ctrl-Z byte ending the read) is not reproduced; ETags are ASCII.
- A `--create-dirs` directory of the save file that cannot be made reports as `-o`'s does
  (`curl: Error creating directory <dir>`, exit 23); not measured, as curl uses the same
  `create_dir_hierarchy` for both.
- The save file is created (append mode) and closed before the transfer, then reopened truncated
  for each ETag, which is what curl's `ftruncate` + write amounts to; a reopen that fails fails the
  header write (exit 23).
- End to end, the built `curl.exe` gave byte-identical request bytes to real curl for the header
  order and missing-file cases, the same wrapped warning, the same saved files, and the same
  single-URL refusal.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --etag-save saves a 2xx/3xx response's ETag and --etag-compare sends If-None-Match, as curl 8.21.0 does
