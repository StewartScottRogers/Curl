---
id: BL-300
title: Supply -w url.<part> and urle.<part> from CurlUrl in TransferWriteOutVariables
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-292]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-300 — Supply -w url.<part> and urle.<part> from CurlUrl in TransferWriteOutVariables

## Goal

`TransferWriteOutVariables` prints `%{url.<part>}` from the URL as given and `%{urle.<part>}` from the effective URL, for the parts `scheme`, `user`, `password`, `options`, `host`, `port`, `path`, `query`, `fragment` and `zoneid`, as curl 8.21.0 does.

## Context

- Measured on 2026-09-26 with curl 8.21.0 (mingw, Schannel); commands and bytes are in BL-284's Notes. ADR-0041 records why BL-284 left these unknown.
- curl's `tool_writeout.c` `urlpart()` parses `per->url` for `url.` and `CURLINFO_EFFECTIVE_URL` for `urle.` with `CURLU_GUESS_SCHEME|CURLU_NON_SUPPORT_SCHEME`, and gets the port with `CURLU_DEFAULT_PORT`; an unknown part name stays an unknown variable.
- Measured: `file:///Z:/bl284tmp/wo.txt` printed scheme `file`, host empty, port `0`, path `Z:/bl284tmp/wo.txt`; `http://u:p@127.0.0.1:18284/wo.txt?q=1#frag` printed `http|u|p||127.0.0.1|18284|/wo.txt|q=1|frag|` for scheme..zoneid; `127.0.0.1:18284` (no scheme) printed scheme `http`, path `/`.
- Parse with `CurlUrl` from BL-292, not `System.Uri`.

## Acceptance criteria

- [ ] Each part of `url.` and `urle.` renders as measured for the file:// and http:// cases above, pinned in `TransferWriteOutVariablesTests`.
- [ ] `%{url.bogus}` is still reported unknown.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Output`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Filed by BL-284.
