---
id: BL-2987
title: Keep the HTTPS record's mandatory key so -v's HTTPS-RR line writes mandatory-keys(ignored)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServiceBindingRecord.cs, Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs, Curl.Networking.UnitLibrary/HttpsRecordReportLine.cs, Curl.Networking.UnitTests/ServiceBindingRecordDecoderTests.cs, Curl.Networking.UnitTests/HttpsRecordReportLineTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2987 — Keep the HTTPS record's mandatory key so -v's HTTPS-RR line writes mandatory-keys(ignored)

## Goal

Under `-v`, the `HTTPS-RR <host>:<port>: <record>` line (BL-2980) and the `[DNS] [HTTPS] record:` lines include ` mandatory-keys(ignored)` when the HTTPS record had a `mandatory` (key 0) parameter, as curl 8.22.0's `Curl_httpsrr_print` writes it.

## Context

Found by BL-2980 (ADR-0727's BL-2980 amendment). `ServiceBindingRecord` does not keep whether the record had a `mandatory` key, so `HttpsRecordReportLine.Format` passes `hasMandatoryKeys: false` to `DohTraceLines.FormatHttpsRecord`. Keep the flag on the decoded record (e.g. a `HasMandatoryKeys` property set by `ServiceBindingRecordDecoder`) and use it in `HttpsRecordReportLine`.

## Acceptance criteria

- [x] A unit test in `HttpsRecordReportLineTests` pins `HTTPS-RR <host>:<port>: 1 . mandatory-keys(ignored) alpn=h2` for a record with a `mandatory` key and `alpn=h2`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- `ServiceBindingRecord.HasMandatoryKeys` is set by `ServiceBindingRecordDecoder` on a key-0 parameter (its value is ignored, as curl ignores the keys it names); `HttpsRecordReportLine` passes it to `DohTraceLines.FormatHttpsRecord`. The `[DNS] [HTTPS] record:` trace lines already tracked key 0 themselves in `DohTraceLines.HttpsRecord.cs`, so they needed no change.
- Measured: build clean, all fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 36ac6bfc 2a446683.
- 2026-10-21: Doing -> Done. HTTPS-RR -v line now writes mandatory-keys(ignored) for a record with a mandatory list
