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
completed:
---
# BL-1172 — Refuse an unknown --ech mode with exit 43 setopt got bad argument as curl does

## Goal

`curl --ech bogus <url>` exits 43 with `curl: (43) setopt 0x2855 got bad argument`, as curl 8.21.0's ECH build does. That build was measured on 2026-10-02 in BL-1107, with and without `ecl:`.

## Context

- ADR-0359 (BL-1107). libcurl's `setopt_ech` returns `CURLE_BAD_FUNCTION_ARGUMENT` for a value other than `false`, `grease`, `true`, `hard`, `ecl:...` or `pn:...`.
- First check what the platform builds without ECH do with `--ech bogus`: the mingw Schannel 8.21.0 build and WSL's OpenSSL 8.18.0 build. Curl matches the platform's curl. If they differ from the ECH build, pin each platform's answer and record which one Curl follows in an ADR.
- Keep `--ai-help` right.

## Acceptance criteria

- [ ] `Curl.Cli.UnitTests` pin the measured exit code and message for an unknown `--ech` mode.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Cli.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-10-02 (lane 3) measured the platform builds without ECH: mingw Schannel 8.21.0 and WSL's OpenSSL 8.18.0 both refuse every `--ech` value, `bogus` included, at parse time with `curl: option --ech: the installed libcurl version does not support this` and the `curl --help` hint, exit 2. Curl already departs from that on purpose: ADR-0327 makes `--ech` work on every platform, so it behaves as an ECH build, and the ECH build's answer (exit 43, `setopt 0x2855 got bad argument`, BL-1107) is the one to match. Record that choice in an ADR when the work lands.
- The refusal is a setopt-time failure, like `--interface`'s malformed value (`setopt 0x274e got bad argument`): libcurl rejects the value in `config2setopts`, after parsing. Curl handles that in `Curl.Console/CurlCommandRunner.cs` (`RefuseMalformedInterface`: `-v` info line, exit 43, `-w` output still written). So the fix needs `Curl.Cli.UnitLibrary` to say the mode is not one libcurl accepts (`false`, `grease`, `true`, `hard`, any case as `setopt_ech` compares) and `Curl.Console` to refuse the transfer the same way. Added `Curl.Console` and `Curl.Console.UnitTests` to `touches` for that. BL-1150 (in Doing) touches `Curl.Console`, so the task went back to Backlog until they no longer overlap.
- Not yet measured with the ECH build: whether `-w` output is written and whether `-v` prints `* setopt 0x2855 got bad argument`. Follow the `--interface` measurement (both happen) unless a re-measure in the BL-1107 Docker build says otherwise.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Backlog. Needs Curl.Console (setopt-time refusal beside RefuseMalformedInterface), which BL-1150 in Doing touches
