---
id: BL-450
title: Word TLS messages, SSL data and trust lines in --trace for the OpenSSL build
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-405]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-450 — Word TLS messages, SSL data and trust lines in --trace for the OpenSSL build

## Goal

With `TlsBackend.OpenSsl`, `TraceTransferEventWriter` writes what curl 8.21.0's OpenSSL
build writes to `--trace` and `--trace-ascii` for `ReportTlsMessage`, `ReportTlsData` and
`ReportTlsTrust`: the `== Info:` lines and the `=> Send SSL data` / `<= Recv SSL data` dumps.

## Context

- BL-405 made `VerboseTransferEventWriter` word these for `-v` (`OpenSslMessageText`,
  `OpenSslTrustText`); `TraceTransferEventWriter` still writes nothing for them (its
  `ReportTlsData` is empty and it takes the default `ReportTlsMessage` and `ReportTlsTrust`).
- Measure first: `curl --trace-ascii - -k https://...` with `curlimages/curl:8.21.0` against
  `openssl s_server -www`, as in BL-405's Notes.

## Acceptance criteria

- [ ] A test in `Curl.Output.UnitTests` pins the `--trace-ascii` lines of one measured
      TLS 1.3 handshake, recorded in this task's Notes; the Schannel tests still pass.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Output`.

## Notes

- Filed from BL-405 (2026-09-27).

## Log

- 2026-09-27: Created.
