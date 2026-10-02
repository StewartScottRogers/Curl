---
id: BL-906
title: Report the early-data bytes sent in %{tls_earlydata}
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-710]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed:
---
# BL-906 — Report the early-data bytes sent in %{tls_earlydata}

## Goal

`%{tls_earlydata}` prints the number of TLS 1.3 early-data bytes the transfer sent, as the platform's curl 8.21.0 prints it, instead of the fixed `0` ADR-0043 pins.

## Context

- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps `tls_earlydata` to a constant `0` (ADR-0043 and its 2026-09-29 amendment, BL-664). That is right only while nothing sends early data: `--tls-earlydata` is parsed (`CommandLineOptions.TlsEarlyData`) and mapped to `TlsClientOptions.AllowEarlyData`, but no provider applies it until BL-710.
- Once BL-710 sends 0-RTT data, the count has to travel from the TLS client to the report (a `TransferReport` or `ConnectResult` member in Abstractions), as BL-661 does for the verify result.
- Measure first with `Record-CurlExchange.ps1 -Tls -k`: two runs sharing `--ssl-sessions f`, the second with `--tls-earlydata -w '%{tls_earlydata}'`, on each platform's curl build (Schannel on Windows prints 0 because it sends no early data).

## Acceptance criteria

- [ ] Measured first as above; stdout copied into Notes for each platform build measured.
- [ ] A test in `Curl.Output.UnitTests` pins `%{tls_earlydata}` to the reported byte count, and tests pin the measured value per platform with `[OSCondition]`.
- [ ] ADR-0043's amendment is updated, or amended again, to say where `tls_earlydata` now comes from.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
