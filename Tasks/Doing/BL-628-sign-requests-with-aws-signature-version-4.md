---
id: BL-628
title: Sign requests with AWS Signature Version 4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-628 — Sign requests with AWS Signature Version 4

## Goal

An `AwsSigV4Signer` in `Curl.Authentication.UnitLibrary` builds the canonical request, string to sign, signing key and `Authorization` header (plus `X-Amz-Date` and, where curl adds it, `x-amz-content-sha256`) exactly as curl 8.21.0 does for a given `--aws-sigv4 "provider1[:provider2[:region[:service]]]"` value, key pair, method, URL, headers, body hash and time.

## Context

- Conformance audit 2026-09-28, row 22 (Major, M). Wiring is BL-629.
- The algorithm: AWS's published Signature Version 4 documentation and its test suite; curl's variations (provider names other than `aws` change the header prefixes and algorithm name, how region and service are taken from the host when not given) are in `CurlManual.txt` (`--aws-sigv4`) and must be confirmed by measurement.
- `HMACSHA256` and `SHA256` from the BCL; inject the time.
- Measure with `Record-CurlExchange.ps1`: `--aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET` for a GET with a query, a POST with `-d`, and `--aws-sigv4 osc` with the host naming region and service; record `request.bin` (the date is in the request, so the test can recompute with it).

## Acceptance criteria

- [ ] Measured first as above; request bytes copied into Notes.
- [ ] `Curl.Authentication.UnitTests` reproduce each measured `Authorization` header byte for byte from the measured date, and pass AWS's published example vectors.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
