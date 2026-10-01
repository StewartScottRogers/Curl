---
id: BL-1088
title: Write the Schannel build's -v schannel: line before a --cert load failure
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1088 — Write the Schannel build's -v schannel: line before a --cert load failure

## Goal

Under the Schannel wording, `-v -k --cert nosuch.pem https://127.0.0.1:port/` writes `* schannel: disabled automatic use of client certificate` before `* schannel: Failed to get certificate location or file for nosuch.pem`, as curl 8.21.0 (mingw, Schannel) does.

## Context

- Found by BL-1083. Measured with `Record-CurlExchange.ps1 -Reset -CurlArgs -v,-k,--cert,nosuch.pem,https://127.0.0.1:18431/`: curl writes the `disabled automatic use` line, then the certificate failure, and exits 58. It does not write the `using IP address` SNI line (curl fails before `schannel_connect_step1` gets that far).
- `SslStreamTlsProvider.AuthenticateAsClientAsync` and `HandBuiltTlsProvider` report `TlsTrustEvent` (which `SchannelTrustText` words) only after `LoadClientCertificate` succeeds, so a load failure writes no `schannel:` line. The OpenSSL build's `SSL Trust` lines may need the existing order; measure before moving the report.

## Acceptance criteria

- [ ] A test in Curl.Networking.UnitTests pins that under the Schannel build a `--cert` load failure reports the client-certificate trust line before failing with exit 58, and without the SNI line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member for each library changed.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
