---
id: BL-2368
title: Add CURLOPT_CERTINFO and fill CURLINFO_CERTINFO from the peer certificates
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2361]
touches: [Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl, Libcurl.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2368 — Add CURLOPT_CERTINFO and fill CURLINFO_CERTINFO from the peer certificates

## Goal

With `CURLOPT_CERTINFO` set on a handle, `Info.CertInfo` after an `https` perform holds one list of `name:value` strings per peer certificate, as libcurl's `CURLINFO_CERTINFO` gives them.

## Context

Left by BL-2361 (ADR-0506 plan phase 2): `Curl.Api`'s `EasyTlsOptions` has no `CURLOPT_CERTINFO` yet. `TransferReport.PeerCertificates` already holds the chain's DER bytes; `Curl.Output`'s `PeerCertificateText` words them as `%{certs}` prints them, and each of its lines becomes one `name:value` string. Match the platform's curl (Schannel build on Windows, OpenSSL build elsewhere) and measure real curl's `%{certs}` output before pinning names.

## Acceptance criteria

- [x] `EasyTlsOptions` holds `CURLOPT_CERTINFO` with libcurl's default (off), as `GatherCertificateInfo`; the `Libcurl` setopt that maps `CURLOPT_CERTINFO` to it comes with `Libcurl`'s option table (ADR-0506 phase 3, filed by BL-2321), since `Libcurl` has no code yet (see Notes).
- [x] A `ManagedEasyTransferTests` or `TransferInfoFromReportTests` test shows the lists filled under the option and empty without it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library <every library changed>` reports 0 failing members.

## Notes

- The option already existed: BL-2302's `EasyTlsOptions.GatherCertificateInfo` documents itself as `CURLOPT_CERTINFO`, default `false`. Kept that name rather than add a second property `CertInfo` for the same option (one concept, one name); the first criterion is reworded to say so.
- `Libcurl` is still an empty project (its CLAUDE.md: "Empty of logic until phase 4"), with no `SetOption` table to add a row to. ADR-0506's plan gives `Libcurl` one hand-written table that writes every `CURLOPT_*` into the easy option records; that table is phase 3 work, whose tasks BL-2321 files, and it maps `CURLOPT_CERTINFO` with the rest. Nothing here touched `Libcurl` or `Libcurl.UnitTests`.
- `TransferInfoFromReport.Of` takes `certificateInfoRequested` (`ManagedEasyTransfer` passes `options.Tls.GatherCertificateInfo`): one list per `TransferReport.PeerCertificates` entry, the server's first. Each list is `PeerCertificateText.Format`'s `%{certs}` text split back into libcurl's strings: each `label:value` line one string, and the PEM block, which curl's `%{certs}` prints with the `Cert:` label stripped, the last string as `Cert:` plus the block with its line feeds (libcurl's `Curl_ssl_push_certinfo_len(..., "Cert", ...)`). A certificate `PeerCertificateText` cannot read gives an empty list. No new curl measurement: the record text is `PeerCertificateText`'s, already pinned byte for byte against real curl (ADR-0054); the Schannel and OpenSSL builds both fill CERTINFO from `x509asn1.c`.
- Tests: `TransferInfoFromReportTests.Of_CertificateInfoRequested_GivesOneListPerPeerCertificate`, `Of_CertificateInfoNotRequested_LeavesTheListsEmpty`, `CertInfoRecordsOf_CertificateText_GivesEachLineAndThePemAsCert`, `CertInfoRecordsOf_UnreadableCertificate_GivesNoStrings` (self-signed P-256 certificates made in memory).
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 100% line, 100% branch, 601 members, 0 failing, worst CRAP 10 (the first run put `Of` at complexity 12; the CERTINFO choice moved into `CertInfoOf`).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. With CURLOPT_CERTINFO (EasyTlsOptions.GatherCertificateInfo) set, Info.CertInfo holds one list of libcurl's name:value strings per peer certificate
