---
id: BL-652
title: Write the --libcurl source file skeleton for a command line
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-652 — Write the --libcurl source file skeleton for a command line

## Goal

`--libcurl <file>` (and `-` for standard output) writes curl 8.21.0's C source file for the command line: the header comment, includes, `main`, `curl_easy_init`, the `CURLOPT_URL` line and the lines curl always emits (buffer size, user agent, max redirects and the like), `curl_easy_perform`, cleanup and return, byte for byte, with the transfer still performed.

## Context

- Conformance audit 2026-09-28, row 30 (Major, L; split: this skeleton, BL-653 HTTP/output/connection options, BL-654 the rest; filed Low).
- The generator maps command-line options, so it belongs in `Curl.Cli.UnitLibrary` (a new type fed `CommandLineOptions`); `Curl.Console` writes the file after the transfers.
- Measure with `Record-CurlExchange.ps1`: `--libcurl - http://127.0.0.1:<P>/`, `--libcurl out.c` with two URLs, and with `-s`; copy the generated text into Notes. Lines naming the curl version must say 8.21.0 as curl's do.

## Acceptance criteria

- [ ] Measured first as above; the generated source copied into Notes.
- [ ] `Curl.Cli.UnitTests` reproduce the measured skeleton byte for byte for the one-URL and two-URL cases; `Curl.Console.UnitTests` show the file written and the transfer performed.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
