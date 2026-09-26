---
id: BL-090
title: Pass -r, -C and --max-filesize from the command line into each transfer in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-013]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-090 — Pass -r, -C and --max-filesize from the command line into each transfer in Curl.Console

## Goal

`curl -r 0-4 file:///...`, `curl -C 5 ...` and `curl --max-filesize 9 ...` through
`Curl.Console` behave as curl 8.21.0 does, because `CurlCommandRunner.CreateContext` passes
the parsed values into each `TransferContext`.

## Context

BL-013 added `-r`/`--range`, `-C`/`--continue-at` and `--max-filesize` to the option table
(`CommandLineOptions.Range`, `ResumeFrom`, `ResumeFromOutputSize`, `MaxFileSize`),
`ByteRangeParser` in `Curl.Core.UnitLibrary`, and `ITransferContext.MaxFileSize`, which
the `file` handler enforces. `Curl.Console` was not in BL-013's `touches`, so
`CurlCommandRunner.CreateContext` does not yet copy them: today the console accepts the
three options and ignores them, transferring the whole file. It also prints
`CommandLineParseResult.WarningLines` nowhere that BL-013 checked; confirm the range warnings
reach standard error. Measured behaviour is in BL-013's Notes and in
`Curl.Cli.UnitTests/CommandLineRangeOptionTests.cs`.

## Acceptance criteria

- [ ] `CreateContext` sets `Range` from `ByteRangeParser.TryParse(options.Range)`; when that
      returns `false` the transfer is not dispatched and ends with
      `ByteRangeParser.NotDeliveredFailure`, printed as
      `curl: (33) Requested range was not delivered by the server`. A test pins `-r 3-1`,
      `-r abc` and `-r -0` through `CurlCommandRunner`.
- [ ] `ResumeFrom` and `MaxFileSize` are passed through; `--max-filesize 9` on a ten-byte
      `file://` source writes nine bytes and exits 63 with
      `curl: (63) Exceeded the maximum allowed file size (9) with 9 bytes`.
- [ ] `-C -` resumes from the size of the `-o` file (or is recorded as a deliberate gap in
      this task's Notes with the upstream behaviour measured).
- [ ] The `-r` warning lines reach standard error before any transfer output.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
