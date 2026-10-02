---
id: BL-1172
title: Refuse an unknown --ech mode with exit 43 setopt got bad argument as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1172 — Refuse an unknown --ech mode with exit 43 setopt got bad argument as curl does

## Goal

`curl --ech bogus <url>` exits 43 with `curl: (43) setopt 0x2855 got bad argument`, as curl 8.21.0's ECH build does. That build was measured on 2026-10-02 in BL-1107, with and without `ecl:`.

## Context

- ADR-0359 (BL-1107). libcurl's `setopt_ech` returns `CURLE_BAD_FUNCTION_ARGUMENT` for a value other than `false`, `grease`, `true`, `hard`, `ecl:...` or `pn:...`.
- First check what the platform builds without ECH do with `--ech bogus`: the mingw Schannel 8.21.0 build and WSL's OpenSSL 8.18.0 build. Curl matches the platform's curl. If they differ from the ECH build, pin each platform's answer and record which one Curl follows in an ADR.
- Keep `--ai-help` right.

## Acceptance criteria

- [x] `Curl.Cli.UnitTests` pin the measured exit code and message for an unknown `--ech` mode.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-10-02 (lane 3) measured the platform builds without ECH: mingw Schannel 8.21.0 and WSL's OpenSSL 8.18.0 both refuse every `--ech` value, `bogus` included, at parse time with `curl: option --ech: the installed libcurl version does not support this` and the `curl --help` hint, exit 2. Curl already departs from that on purpose: ADR-0327 makes `--ech` work on every platform, so it behaves as an ECH build, and the ECH build's answer (exit 43, `setopt 0x2855 got bad argument`, BL-1107) is the one to match. Record that choice in an ADR when the work lands.
- The refusal is a setopt-time failure, like `--interface`'s malformed value (`setopt 0x274e got bad argument`): libcurl rejects the value in `config2setopts`, after parsing. Curl handles that in `Curl.Console/CurlCommandRunner.cs` (`RefuseMalformedInterface`: `-v` info line, exit 43, `-w` output still written). So the fix needs `Curl.Cli.UnitLibrary` to say the mode is not one libcurl accepts (`false`, `grease`, `true`, `hard`, any case as `setopt_ech` compares) and `Curl.Console` to refuse the transfer the same way. Added `Curl.Console` and `Curl.Console.UnitTests` to `touches` for that. BL-1150 (in Doing) touches `Curl.Console`, so the task went back to Backlog until they no longer overlap.
- Not yet measured with the ECH build: whether `-w` output is written and whether `-v` prints `* setopt 0x2855 got bad argument`. Follow the `--interface` measurement (both happen) unless a re-measure in the BL-1107 Docker build says otherwise.
- 2026-10-02 (lane 1) delivered. curl 8.21.0's `lib/setopt.c` `setopt_ech` (read at tag `curl-8_21_0`) accepts `false`, `grease`, `true` and `hard` with `strcmp`, and `ecl:` (length > 4) and `pn:` (length > 3) with `strncmp`. The match is case-sensitive, so `TRUE` and `PN:x` are refused too, while `pn:x` and `ecl:x` (which the tool's own split leaves as the mode) are accepted. `CommandLineOptions.EchModeIsMalformed` encodes that, and `Curl.Cli.UnitTests` pin which modes it refuses. The exit code and message live in the console layer, so `Curl.Console.UnitTests/CurlCommandRunnerEchModeTests.cs` pins exit 43 and `curl: (43) setopt 0x2855 got bad argument` (with and without `ecl:`, `-v`, `--next`). `CurlCommandRunner.RefuseMalformedEchMode` runs right after `RefuseMalformedInterface` (interface checked first; that order was not measured). ADR-0378 records following the ECH build over the platform builds' exit 2.
- `--ai-help` needed no change: its `--ech` section comes from curl's manual, which already lists the valid modes. Measured coverage of the new code in `Curl.Cli.UnitLibrary`: 100% lines, 16/16 and 2/2 branches.
- Gap not fixed here (outside this task's `touches`): `Curl.Networking`'s `EchModes.Of` treats a mode of `pn:x` or `ecl:x` (too short for the tool's own split) as no mode, where libcurl takes it as a public name or list. It is an edge case and is left as it is.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Backlog. Needs Curl.Console (setopt-time refusal beside RefuseMalformedInterface), which BL-1150 in Doing touches
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. An unknown --ech mode fails the transfer with exit 43 and setopt 0x2855 got bad argument, as curl's ECH build does (ADR-0378)
