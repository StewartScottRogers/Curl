---
id: BL-405
title: Write the OpenSSL build's -v TLS record, SSL Trust and subjectAltName lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-356]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-405 — Write the OpenSSL build's -v TLS record, SSL Trust and subjectAltName lines

## Goal

With `TlsBackend.OpenSsl`, `VerboseTransferEventWriter` also writes the lines the OpenSSL
build of curl 8.21.0 prints around a handshake that BL-356 left out: the TLS record lines
(`* TLSv1.3 (OUT), TLS handshake, Client hello (1):` and their `} [N bytes data]`), the
`SSL Trust` lines, the `subjectAltName: "host" matches cert's "host"` line, and
`Proxy certificate:` for a proxy's handshake.

## Context

- ADR-0085 lists these as left out: `ReportTlsData` writes nothing, the handshake event
  carries no host name or proxy flag, and the `SSL Trust` lines come from the connect
  options (`--cacert`, `-k`), not the handshake.
- Measured lines and source references are in BL-356's Notes (`lib/vtls/openssl.c`
  `ossl_verifyhost`, `ossl_trace`; `lib/vtls/vtls.c` for `SSL Trust`). Measure the TLS
  record lines with `Record-CurlExchange.ps1` or Docker `curlimages/curl:8.21.0` first.
- May need new optional properties on `TlsHandshakeEvent` (host name, is-proxy) and a
  record-type argument on `ReportTlsData`; decide in an ADR.

## Acceptance criteria

- [ ] Tests in `Curl.Output.UnitTests` pin each of the four kinds of line against a
      measurement recorded in this task's Notes; the Schannel tests still pass.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"`
      passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Output`.

## Notes

- Filed from BL-356 (2026-09-27).

## Log

- 2026-09-27: Created.
