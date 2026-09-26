---
id: BL-269
title: Wrap the Schannel --capath warning at the terminal width instead of pre-wrapped at 79
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-072]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-269 — Wrap the Schannel --capath warning at the terminal width instead of pre-wrapped at 79

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

- [ ] `SslStreamTlsProvider.Warnings` holds the single unwrapped line for `--capath` on
      the Schannel build; a named test in `Curl.Networking.UnitTests` asserts it.
- [ ] A named test in `Curl.Console.UnitTests` asserts that at 79 columns standard error
      still gets the two measured lines, each followed by `Environment.NewLine`, and at
      200 columns gets one line.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-26: Created.
