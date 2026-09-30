---
id: BL-661
title: Report the OpenSSL verify result in %{ssl_verify_result} and %{proxy_ssl_verify_result} off Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-661 — Report the OpenSSL verify result in %{ssl_verify_result} and %{proxy_ssl_verify_result} off Windows

## Goal

Off Windows, `%{ssl_verify_result}` and `%{proxy_ssl_verify_result}` print the X509 verify code the OpenSSL build prints (0 when verified, for example 18 for a self-signed certificate under `-k`, 10 for an expired one), while Windows keeps printing 0 as the Schannel build does (ADR-0043).

## Context

- Conformance audit 2026-09-28, row 44 (Major off Windows, "measure first").
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps both to a constant 0; `Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs` already maps chain status to OpenSSL's codes for messages (ADR-0085). The code has to travel from the TLS provider to the report (a `TransferReport` or `ConnectResult` member in Abstractions).
- Measure on Linux or macOS with `Record-CurlExchange.ps1 -Tls`: `-k -w '%{ssl_verify_result}'` against the script's self-signed certificate, the same with `--cacert` for it (verified), and through an HTTPS proxy with `--proxy-insecure -w '%{proxy_ssl_verify_result}'`.

## Acceptance criteria

- [ ] Measured first as above; stdout copied into Notes.
- [ ] Tests pin each measured value under `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`, and 0 under `[OSCondition(OperatingSystems.Windows)]`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
