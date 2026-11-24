---
id: BL-5873
title: Send a Range header for -r and -C - on s3:// downloads, so a ranged or resumed GET returns 206
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Operations.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Configuration.cs, Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpRequest.cs, Curl.Extended.Protocol.S3.UnitTests/Client/S3Operations]
requirement: none
created: 2026-11-24
completed:
---
# BL-5873 — Send a Range header for -r and -C - on s3:// downloads, so a ranged or resumed GET returns 206

## Goal

`-r <range>` and `-C -` (and `-C <offset>`) on an `s3://` download send a signed `Range: bytes=...` header, so the server answers 206 with only the requested bytes, exactly as the same request over `https://...amazonaws.com` with `--aws-sigv4` does.

## Context

From Surly cookbook bug report `2026-11-24T1330-Curl-bug-report.md` (batch 14), finding 2 (Medium). Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11 (seen by the recipe writer; not re-run separately).

Repro: `surl --directory store --cert server.pem --key server.key --user AKIAIOSFODNN7EXAMPLE:s3cret s3://127.0.0.1:P/` with `store/media/big.bin` and a certificate naming `*.s3.us-east-1.amazonaws.com`; `curl -sS --cacert server.pem --connect-to ::127.0.0.1:P -u AKIAIOSFODNN7EXAMPLE:s3cret -r 0-9 -o part.bin -w "%{http_code} %{size_download}\n" s3://media/big.bin` prints `200` and the whole size; expected `206 10`. `-C -` on a partial file sends no `Range` either. Start in `Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Operations.cs` and `S3Configuration.cs` (carry the transfer's range/resume offset) and `Http/S3HttpRequest.cs` (write the header, and include it in the SigV4 signed headers as the https path does). A resumed download must append from the offset and report `size_download` as the bytes received, as over https.

## Acceptance criteria

- [ ] `-r 0-9` on an `s3://` GET sends `Range: bytes=0-9`, and the 206 body (10 bytes) is written, pinned by a unit test against a test-double connection.
- [ ] `-C -` with a partial output file sends `Range: bytes=<size>-` and appends; `-C <n>` sends `bytes=<n>-`, each pinned by a unit test.
- [ ] A 200 reply to a ranged request behaves as curl's https path does (measure it; record it in Notes), pinned by a test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from Surly bug report 2026-11-24T1330, finding 2.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 944fda24 d8ed0a1f 575b2b3a 3d836d5e.
