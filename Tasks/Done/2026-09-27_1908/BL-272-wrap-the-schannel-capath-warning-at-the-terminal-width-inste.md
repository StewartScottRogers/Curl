---
id: BL-272
title: Wrap the Schannel --capath warning at the terminal width instead of pre-wrapped at 79
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-072]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-272 — Wrap the Schannel --capath warning at the terminal width instead of pre-wrapped at 79

## Goal

On a terminal wider than 79 columns, the Schannel build's `--capath` warning is printed
wrapped at the terminal width, as curl 8.21.0 wraps every `Warning: ` message, instead of
always as the two lines curl prints at 79 columns.

## Context

- `SslStreamTlsProvider.Warnings` (`Curl.Networking.UnitLibrary`) holds the message
  already split into the two lines measured through a pipe (ADR-0009):
  `Warning: ignoring setting the CA path for the proxy, not supported by libcurl ` and
  `Warning: with Schannel`. That is one curl message,
  `ignoring setting the CA path for the proxy, not supported by libcurl with Schannel`,
  wrapped by curl's `warnf`.
- BL-072 prints those lines before each transfer through `CurlCommandRunner`, whose
  `WarningLineWrapper` wraps at `TerminalColumns`; two pre-wrapped lines stay two lines at
  any width, so a wide terminal differs from curl.
- Fix: expose the unwrapped message (one `Warning: ...` line) from the provider and let
  the runner's wrapper split it. Measure real curl 8.21.0 (mingw, Schannel) in a wide
  terminal first to confirm it prints one line there.

## Acceptance criteria

- [x] `SslStreamTlsProvider.Warnings` holds the single unwrapped line for `--capath` on
      the Schannel build; a named test in `Curl.Networking.UnitTests` asserts it.
- [x] A named test in `Curl.Console.UnitTests` asserts that at 79 columns standard error
      still gets the two measured lines, each followed by `Environment.NewLine`, and at
      200 columns gets one line.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Measured 2026-09-27 with the local curl 8.21.0 (x86_64-w64-mingw32, Schannel):
  `COLUMNS=200 curl --no-progress-meter --capath . file:///C:/Windows/win.ini -o NUL`
  prints the one line `Warning: ignoring setting the CA path for the proxy, not supported by
  libcurl with Schannel`; `COLUMNS=79` prints the two lines ADR-0009 recorded.
- Small enough to deliver directly rather than through the full `/feature` stages: the
  runner already wraps every `Warning: ` line it writes through `WarningLineWrapper`, so
  the fix is only the provider's text. `CurlComposition` needed no change.
- Tests: `SslStreamTlsProviderTests.Warnings_WithCaCertificateDirectoryInTheSchannelBuild_AreTheOneUnwrappedLineSchannelCurlWarns`,
  `CurlCommandRunnerTransferWarningTests.RunAsync_CaPathWarningAt79Columns_PrintsTheTwoMeasuredLines`
  and `RunAsync_CaPathWarningAt200Columns_PrintsOneLine`.
- Added `Documentation/Planning/Decisions/ADR-0009-...` to `touches`: its section 2 said
  stderr gets "exactly two lines", no longer true of the code on a wide terminal. No task
  in Doing names it. Updated the ADR, `TransferDispatch`'s doc comment and the Networking
  `CLAUDE.md` to say one warning, wrapped at the terminal width.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The Schannel --capath warning wraps at the terminal width: two lines at 79 columns, one at 200
