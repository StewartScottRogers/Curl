---
id: BL-1150
title: Print the early data bytes sent in --write-out tls_earlydata
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1105]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1150 — Print the early data bytes sent in --write-out tls_earlydata

## Goal

`-w '%{tls_earlydata}'` prints the number of bytes the transfer sent as TLS 1.3 0-RTT early data (BL-1105), as curl 8.21.0's OpenSSL build does, instead of the fixed 0 it prints today.

## Context

- Follow-up from BL-1105 (ADR-0337): `HandBuiltTlsProvider.HandshakeWithEarlyDataAsync` knows the bytes sent and whether the server accepted them, but nothing carries the count to the transfer's result.
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps `tls_earlydata` to `WriteOutValue.FromNumber(0)`.
- curl 8.21.0: `lib/vtls/openssl.c` `ossl_send_earlydata` records `connssl->earlydata_skip`; `lib/getinfo.c` `CURLINFO_EARLYDATA_SENT_T` reports the bytes sent, accepted or not (check the source at tag `curl-8_21_0` before pinning).

## Acceptance criteria

- [ ] A test pins `%{tls_earlydata}` as the early data byte count after a resumed `--tls-earlydata` transfer, and 0 without early data.
- [ ] The value reaches the write-out through the transfer result, not a static.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-10-01: Created.
