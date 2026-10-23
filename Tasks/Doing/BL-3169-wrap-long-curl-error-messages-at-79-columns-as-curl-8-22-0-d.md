---
id: BL-3169
title: Wrap long curl: error messages at 79 columns as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitLibrary/WrappedMessage.cs, Curl.Cli.UnitTests/CommandLineRefusalWrappingTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3169 — Wrap long curl: error messages at 79 columns as curl 8.22.0 does

## Goal

Long `curl:` error messages are wrapped across lines exactly as curl 8.22.0 wraps them, byte for byte.

## Context

Stewart reported on 2026-10-22 that curl wraps long `curl:` messages at spaces. Measured the same day on Windows with System32 curl 8.21.0 Schannel: `curl -sS --cacert /some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate-authority-bundle.pem https://x/` prints three lines, `curl: The file `, `curl: '/some/very/long/missing/path/that/goes/on/and/on/for/a/while/certificate` (79 chars) and `curl: -authority-bundle.pem' provided to --cacert does not exist`. So curl wraps at 79 columns and breaks inside a long token, not only at spaces (the report said "at spaces"; the measurement says otherwise). Curl prints one 146-character line. Curl already wraps `Warning:` lines and, for some `curl:` lines, via `Curl.Cli.UnitLibrary/WrappedMessage.cs` (a port of `voutf` in tool_msgs.c). The message in question is built in `Curl.Cli.UnitLibrary/CommandLineRefusal.cs` near line 377. Measure curl 8.22.0 (not 8.21.0) with `Record-CurlExchange.ps1` for ten long-path cases and find upstream's wrapping rule in tool_msgs.c; apply `WrappedMessage` to every `curl:` message upstream wraps. If the wrapping must be applied where `CurlCommandRunner` prints the line, only add the narrowest change needed there (it is a very large file other tasks edit; add it to `touches` before editing it).

Also note: on the same run real Windows curl 8.21 printed `curl: try 'curl --help' for more information` (no `curl --manual`), while Curl prints `curl: try 'curl --help' or 'curl --manual' for more information` (`CommandLineRefusal.TryHelpLine`). Measure 8.22.0's Windows build. If its try-help line differs by platform, do not change the constant in this task: file that as its own task. It touches the same constant as BL-3145 (`Curl.Cli.UnitLibrary/CommandLineRefusal.cs`, whose no-arguments line the "small conformance fixes" task reverses), so that follow-up must depend on the conformance-fixes task so one task owns the constant.

## Acceptance criteria

- [ ] Notes record curl 8.22.0's exact bytes for ten long-path cases (different options and path lengths, a path with and without spaces, `COLUMNS` unset, 40 and 200), and the wrapping rule found in tool_msgs.c.
- [ ] `Curl.Cli.UnitTests/CommandLineRefusalWrappingTests.cs` has the ten cases, each comparing Curl's standard-error lines byte for byte with the recorded ones.
- [ ] The long `--cacert` case produces the three lines quoted above (or 8.22.0's measured equivalent) with each line at most 79 bytes.
- [ ] Short messages and messages that are wrapped today (`Warning:` lines) are unchanged: the existing `WrappedMessage` and refusal tests still pass.
- [ ] Notes say whether curl 8.22.0's try-help line differs on Windows from Linux/macOS, and name the follow-up task filed if it does.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 6af107f0 d3cdbed2 0471c273 d1212222 61cabad7 ae25be8d.
