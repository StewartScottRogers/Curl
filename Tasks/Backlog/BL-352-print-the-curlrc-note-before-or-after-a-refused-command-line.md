---
id: BL-352
title: Print the .curlrc note before or after a refused command line as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-243]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-352 — Print the .curlrc note before or after a refused command line as curl does

## Goal

`Curl.Console` prints `Note: Read config file from '<path>'` for a refused command line exactly where curl 8.21.0 does, when `-v` or a `--trace` option was read before the refusal.

## Context

- Found in BL-243 (2026-09-27). BL-243 prints the note for accepted command lines and `-V`, from `CommandLineOptions.DefaultConfigFile` and `Trace`. A refused `CommandLineParseResult` carries no options, so the runner cannot tell whether a `.curlrc` was read or `-v` was on.
- Measured on curl 8.21.0 (mingw, Schannel), `CURL_HOME` naming a directory with a `.curlrc`:
  - `curl -v --bogus http://127.0.0.1:1/` prints `curl: option --bogus: is unknown` and `curl: try 'curl --help' or 'curl --manual' for more information`, then the note.
  - `curl --bogus -v http://127.0.0.1:1/` prints no note (the `-v` was never read).
  - `curl -v` (no URL) prints the note first, then `curl: (2) no URL specified` and the try line.
- Start at `CommandLineParseResult.Refused` in `Curl.Cli.UnitLibrary` and `CurlCommandRunner.RunAsync` / `WriteDefaultConfigFileNoteAsync` in `Curl.Console`.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test pins each of the three measured cases above byte for byte.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
