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
completed: 2026-09-27
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

- [x] A `Curl.Console.UnitTests` test pins each of the three measured cases above byte for byte.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

- Measured again on curl 8.21.0 (mingw, Schannel) on 2026-09-27 with `CURL_HOME` naming a `.curlrc`: every refusal curl meets while reading the command line (`--bogus`, `-o` with no value, `--http2`, `-K` of a missing file, exit 26) prints its lines and then the note; the two it meets at transfer setup print the note first: `curl: (2) no URL specified`, and `-F a=b -d x` (note, then the two `Warning: You can only select one HTTP request method!` lines). A `.curlrc` of `verbose` with no arguments prints the try-help line alone: curl refuses an empty command line before it reads it.
- Design: `CommandLineParseResult.NotedDefaultConfigFile` names the file to announce (accepted or refused; never for the empty command line), and `CommandLineRefusal.FoundAtTransferSetup` (true for `NoUrlSpecified` and `FormAndDataBoth`) tells `CurlCommandRunner.WriteRefusalAsync` whether the note goes before or after the refusal's lines. The runner's accepted path uses the same property. No ADR: every placement is measured, not chosen.
- `FormAndDataBoth` now carries its warning lines as the refusal's own lines (`FormAndDataBoth(warningLines)`, empty under `-s`) instead of appending them to `WarningLines`, so the note can fall between warnings met while reading and this one. Byte output of every existing case is unchanged.
- `Curl.Console` measured with `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration`, as BL-280 set: `DiskWriteOutFileOpener` is covered only by Integration tests. 100% line and branch, 0 failing, worst CRAP 10. `Curl.Cli.UnitLibrary`: 100% / 100%, 0 failing, worst CRAP 10.
- Tests: six in `CurlCommandRunnerConfigFileTests` (the three measured cases byte for byte, form+data, `-K` unreadable, empty command line with a verbose `.curlrc`); five in `CommandLineDefaultConfigFileTests`; the form+data tests in `CommandLineFormOptionTests` now assert the refusal's lines. Cli.UnitTests 1920 passed, Console.UnitTests 730 passed.
- `dotnet format --verify-no-changes` reports end-of-line errors only in files this task did not touch (`CurlComposition.cs`, `PhysicalFileSystem.cs`, Networking, Dict, Telnet), left for their owners.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A refused command line prints the .curlrc note after parse-time refusals and before no-URL and form+data refusals, as curl 8.21.0 does
