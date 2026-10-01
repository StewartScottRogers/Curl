---
id: BL-653
title: Write the --libcurl setopt lines for the HTTP, output and connection options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-652]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-653 — Write the --libcurl setopt lines for the HTTP, output and connection options

## Goal

The `--libcurl` generator writes curl 8.21.0's `curl_easy_setopt` lines (and any `curl_slist`/`curl_mime` set-up and cleanup) for the HTTP options (`-X`, `-H`, `-d` family, `-F`, `-u`, `-L`, `-e`, `-A`, `-b`, `-c`, `--compressed`, `-I`, `-f`), output options (`-o`, `-O`, `-i`) and connection options (timeouts, `--resolve`, `--connect-to`, `-4`/`-6`), in curl's order and formatting.

## Context

- Conformance audit 2026-09-28, row 30. Builds on BL-652.
- Measure each option's lines with `Record-CurlExchange.ps1` (`--libcurl -` with one option at a time, then a combined command line to confirm ordering); copy the text into Notes.

## Acceptance criteria

- [ ] Measured first as above.
- [ ] `Curl.Cli.UnitTests` reproduce each measured output byte for byte (data rows), including the combined case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
