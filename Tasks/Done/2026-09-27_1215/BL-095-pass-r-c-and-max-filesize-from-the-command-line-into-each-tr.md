---
id: BL-095
title: Pass -r, -C and --max-filesize from the command line into each transfer in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-013]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-095 — Pass -r, -C and --max-filesize from the command line into each transfer in Curl.Console

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

- [x] `CreateContext` sets `Range` from `ByteRangeParser.TryParse(options.Range)`; when that
      returns `false` the transfer is not dispatched and ends with
      `ByteRangeParser.NotDeliveredFailure`, printed as
      `curl: (33) Requested range was not delivered by the server`. A test pins `-r 3-1`,
      `-r abc` and `-r -0` through `CurlCommandRunner`.
- [x] `ResumeFrom` and `MaxFileSize` are passed through; `--max-filesize 9` on a ten-byte
      `file://` source writes nine bytes and exits 63 with
      `curl: (63) Exceeded the maximum allowed file size (9) with 9 bytes`.
- [x] `-C -` resumes from the size of the `-o` file (or is recorded as a deliberate gap in
      this task's Notes with the upstream behaviour measured).
- [x] The `-r` warning lines reach standard error before any transfer output.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Measured 2026-09-26 with local curl 8.21.0 (x86_64-w64-mingw32, Schannel) on a ten-byte
  `file://` source `0123456789`:
  - `-r 0-4` writes `01234`, exit 0. `-r 3-1`, `-r abc`, `-r -0` exit 33 with
    `curl: (33) Requested range was not delivered by the server`; `abc` prints its three
    warning lines first; `-s -r abc` prints nothing. `-r 5abc` warns and writes `56789`.
  - `--max-filesize 9 -o out.txt` writes `012345678`, exit 63 with the (63) line.
    `--max-filesize 0` is no limit.
  - `-C 5` to stdout writes `56789`. `-C 5 -o` an existing `XYZ` gives `XYZ56789` (append).
    `-C 0 -o` an existing file replaces it. `-C - -o` an existing `ABCD` gives `ABCD456789`;
    `-C - -o` a missing file transfers the whole resource; `-C -` without `-o` transfers the
    whole resource; `-C - -o` a 12-byte file exits 36 `failed to resume file:// transfer`
    and leaves it untouched; `-C - -o` a directory behaves as a plain `-o` directory
    (`Warning: Failed to open the file d: ...`, `(23) client returned ERROR on write of 10 bytes`).
  - `-C 3 -o d` (directory, or a missing parent directory) prints `curl: cannot open 'd'`
    and `curl: (23) Failed writing received data to disk/application`, exit 23; `-s` hides
    both, `-sS` shows both. curl opens the file before the transfer, and in a two-URL run it
    then stops without transferring the second URL.
  - Without `-s` / `--no-progress-meter`, curl also prints its progress meter, and for
    `-C N` a `** Resuming transfer from byte position N` line; both belong to the progress
    meter, which `Curl.Console` does not print at all.
- Design: `CurlCommandRunner.TransferAsync` parses the range (failure returns
  `NotDeliveredFailure` before any output file is touched, so no file is created, as curl);
  `ResolveResumeFromAsync` answers `-C -` with the `-o` file's `FileOpenResult.Length` via
  `IFileSystem.OpenForReadAsync` (null when it cannot be opened, i.e. the whole resource);
  `TransferToOutputFileAsync` opens the `-o` file with `FileWriteMode.Append` up front when
  the offset is above zero (`DeferredOutputFileStream` gained a write mode and
  `TryOpenNowAsync`), else keeps the deferred truncating open.
- Choice: `RunAsync` now writes every `CommandLineParseResult.WarningLines` line first, on
  accepted and refused command lines, rather than only the range warnings - the parser owns
  which warnings exist and already filters the range ones under an earlier `-s`. This also
  delivers most of BL-088's goal; until BL-093 lands, `-s -o -x` still prints the
  looks-like-a-flag warning that curl hides, which BL-093/BL-088 settle.
- Coverage: every line and branch added here is covered (`dotnet test --collect "Code
  Coverage;Format=cobertura"`). The only uncovered code in `Curl.Console` predates this task:
  `CurlTransports.cs` lines 16-23 and one branch of `TlsClientOptionsMapping.cs` line 31.
- Follow-ups filed: BL-103 (stop the remaining URLs after a resumed `-o` cannot be opened),
  BL-104 (stale BL-090 remark on `ITransferContext.Range`), BL-102 (progress meter and the
  `** Resuming` line).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Curl.Console passes -r, -C (including -C -, appending to the -o file) and --max-filesize into each transfer and prints the parser's warning lines first
