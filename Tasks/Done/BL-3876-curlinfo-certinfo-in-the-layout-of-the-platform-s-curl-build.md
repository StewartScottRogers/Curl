---
id: BL-3876
title: CURLINFO_CERTINFO in the layout of the platform's curl build (OpenSSL records off Windows)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3620]
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.CertInfo.cs, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3876 — CURLINFO_CERTINFO in the layout of the platform's curl build (OpenSSL records off Windows)

## Goal

Off Windows, `curl_easy_getinfo(CURLINFO_CERTINFO)` returns the OpenSSL build's records (`Subject:CN = localhost`, extensions, `Start date:`, `rsa(n):` in `BN_print` hex, `Cert:` PEM), as libcurl's OpenSSL build does; on Windows the x509asn1.c records as now.

## Context

BL-3620 (ADR-0926) made `-w %{certs}` follow the platform: `PeerCertificateText.FormatFor(certificate, PlatformTlsBackend.ForProcess)`. `TransferInfoFromReport.CertInfo` still builds `CURLINFO_CERTINFO` from `PeerCertificateText.Format`, the x509asn1.c layout, and splits that text back into records (`CertInfoRecordsOf`), so on Linux and macOS it disagrees with `%{certs}` and with libcurl's OpenSSL build. Note the OpenSSL layout's authority key identifier value can span several lines, so splitting the `%{certs}` text by line no longer gives one record per label; the records need building directly (or a split that knows the labels). The record labels and order are in ADR-0926; the last record is `Cert` with the PEM.

## Acceptance criteria

- [x] With `TlsBackend.OpenSsl`, `CURLINFO_CERTINFO` holds the OpenSSL build's records for BL-3620's two measured certificates (in `Curl.Output.UnitTests/Cgr091Tests.cs`), one list entry per record, `Cert:` last; with `TlsBackend.Schannel` the records are as now.
- [x] Unit tests in `Curl.Engine.UnitTests/TransferInfoFromReportTests.cs` pin both layouts.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no failing member.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

CertInfoOf takes an optional TlsBackend (null = PlatformTlsBackend.ForProcess); CertInfoRecordsOf folds the OpenSSL authority key identifier's DirName:/serial: continuation lines into its record. Quality run: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 15703830 fbd00c8c a526f176 779790c0.
- 2026-10-28: Doing -> Done. CURLINFO_CERTINFO follows the platform's curl build layout (OpenSSL records off Windows)
