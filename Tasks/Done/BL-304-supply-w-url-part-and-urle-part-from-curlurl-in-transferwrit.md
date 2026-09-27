---
id: BL-304
title: Supply -w url.<part> and urle.<part> from CurlUrl in TransferWriteOutVariables
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-292]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-304 — Supply -w url.<part> and urle.<part> from CurlUrl in TransferWriteOutVariables

## Goal

`TransferWriteOutVariables` prints `%{url.<part>}` from the URL as given and `%{urle.<part>}` from the effective URL, for the parts `scheme`, `user`, `password`, `options`, `host`, `port`, `path`, `query`, `fragment` and `zoneid`, as curl 8.21.0 does.

## Context

- Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel); commands and bytes are in BL-284's Notes. ADR-0043 records why BL-284 left these unknown.
- curl's `tool_writeout.c` `urlpart()` parses `per->url` for `url.` and `CURLINFO_EFFECTIVE_URL` for `urle.` with `CURLU_GUESS_SCHEME|CURLU_NON_SUPPORT_SCHEME`, and gets the port with `CURLU_DEFAULT_PORT`; an unknown part name stays an unknown variable.
- Measured: `file:///Z:/bl284tmp/wo.txt` printed scheme `file`, host empty, port `0`, path `Z:/bl284tmp/wo.txt`; `http://u:p@127.0.0.1:18284/wo.txt?q=1#frag` printed `http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|` for scheme..zoneid; `127.0.0.1:18284` (no scheme) printed scheme `http`, path `/`.
- Parse with `CurlUrl` from BL-292, not `System.Uri`.

## Acceptance criteria

- [x] Each part of `url.` and `urle.` renders as measured for the file:// and http:// cases above, pinned in `TransferWriteOutVariablesTests`.
- [x] `%{url.bogus}` is still reported unknown.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Output`.

## Notes

- Plan: a table of the ten parts, each a reader over `CurlUrl`, added to the variable table twice - `url.` over the URL as given, `urle.` over `url_effective` (the report's `EffectiveUrl`, else the request URL). Names not in the table, such as `url.bogus` and `url.`, stay unknown.
- Parsed without path-as-is, as `urlpart()` calls `curl_url_set` without `CURLU_PATH_AS_IS`. A URL that does not parse, or a part it lacks (no user, no query), prints nothing, as curl prints nothing when `curl_url_get` fails.
- Port (default taken, no ADR needed - it follows `curl_url_get` with `CURLU_DEFAULT_PORT`): the port written, else the scheme default; `CurlUrl.Port` is -1 for `file` and unknown schemes, so `file` prints curl's handler default `0` and an unknown scheme prints nothing (no handler, `CURLUE_NO_PORT`). The unknown-scheme case is from curl's source, not measured.
- The measured http case prints `frag` for `urle.fragment` too, so the test pins an effective URL that keeps the fragment.
- Gates: `dotnet build -warnaserror` clean; fast tests green (Curl.Output 106 passed); `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Filed by BL-284.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -w url.<part> and urle.<part> print the ten URL parts from CurlUrl as curl 8.21.0 does
