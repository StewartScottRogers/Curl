---
id: BL-356
title: Word -v TLS handshake lines as the OpenSSL build does on Linux and macOS
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-228]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-356 — Word -v TLS handshake lines as the OpenSSL build does on Linux and macOS

## Goal

On Linux and macOS, `VerboseTransferEventWriter` renders a `TlsHandshakeEvent` as curl's OpenSSL build does for `-v`: the protocol version and cipher, the ALPN lines and the server certificate fields, as measured.

## Context

- BL-228 added `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, which words TLS facts only as curl 8.21.0's Schannel build does (the two ALPN lines, measured on Windows). ADR-0009 says Linux and macOS match the OpenSSL build, which ADR-0046 records also prints `* SSL connection using TLSv1.3 / ...`, `* Server certificate:` and its fields.
- Measure a Linux or macOS curl 8.21.0 (OpenSSL) `-v -k` against a loopback TLS server first; never pin unmeasured text. How the writer learns the platform (a constructor argument chosen by `Curl.Console`, or `OperatingSystem.IsWindows()`) is this task's decision; record it in Notes.

## Acceptance criteria

- [ ] A test in `Curl.Output.UnitTests` pins the OpenSSL-build `-v` TLS lines for one measured HTTPS exchange, with the command and bytes recorded in Notes; the Schannel tests in `VerboseTransferEventWriterTests` still pass.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Filed from BL-228 (2026-09-27).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
