---
id: BL-1034
title: Warn that --tls13-ciphers and --proxy-tls13-ciphers are ignored on the Schannel build
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-02
---
# BL-1034 — Warn that --tls13-ciphers and --proxy-tls13-ciphers are ignored on the Schannel build

## Goal

On Windows (curl's Schannel build), `--tls13-ciphers` and `--proxy-tls13-ciphers` print curl's "ignoring" warning to standard error, as curl 8.21.0 does, and the transfer carries on.

## Context

- Measured in BL-606 (curl 8.21.0 Schannel, 2026-09-30), with `Record-CurlExchange.ps1 -Tls` on 127.0.0.1:47606:
  - `curl -S -o NUL --insecure --tls13-ciphers BOGUS https://127.0.0.1:47606/` prints `Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel`, exit 0.
  - `curl -S -o NUL -x https://127.0.0.1:47606 --proxy-insecure --proxy-tls13-ciphers BOGUS http://example.com/` prints `Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel`, exit 0.
  - `-s` silences both.
- Curl prints neither today. `SslStreamTlsProvider` already carries Schannel warnings through `ITlsProviderWithWarnings.Warnings` (the `--capath` one); ADR-0011 covers the cipher lists. The OpenSSL build honours both options and prints nothing.
- Measure first when each warning is printed (before connecting, once per transfer or once per run) before pinning it.

## Acceptance criteria

- [x] Real curl measured for both options: when the warning appears relative to the transfer, and how many times for two URLs; recorded in Notes.
- [x] A Console test pins each warning's bytes on Windows (`OSCondition(OperatingSystems.Windows)`), and a test pins no warning off Windows.
- [x] `--ai-help` still describes both options correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Tls` on 127.0.0.1:47634-47641:
  - Two URLs (`-v`, `--tls13-ciphers BOGUS`, and separately `-x https://... --proxy-tls13-ciphers BOGUS`): each warning printed once per URL, before that URL's `Trying` line; exit 0.
  - Plain `http://` URL with no proxy still warns; `-s` and `-sS` print nothing.
  - With `--capath x --proxy-tls13-ciphers B --tls13-ciphers BOGUS`: capath warning (wrapped), then `--tls13-ciphers`, then `--proxy-tls13-ciphers`, whatever the command line's order. At 79 columns neither line wraps (72 and 79 bytes).
- Decision (ADR-0349): `CurlComposition.WarningLinesBeforeEachTransfer` appends the two lines after the proxy TLS provider's warnings in the Schannel build. Kept in `Curl.Console`, so `Curl.Networking.UnitLibrary` is untouched: a provider cannot tell its own option name.
- Tests: `CurlCompositionTests` (Windows-only bytes, off-Windows none, order, each alone, OpenSSL build none) and `CurlCommandRunnerTransferWarningTests` (79 columns, two URLs). `--ai-help tls` already says both options are used only by OpenSSL, wolfSSL and mbedTLS builds; unchanged.
- `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. On Windows, --tls13-ciphers and --proxy-tls13-ciphers print curl's Schannel 'ignoring' warning once per URL before the transfer
