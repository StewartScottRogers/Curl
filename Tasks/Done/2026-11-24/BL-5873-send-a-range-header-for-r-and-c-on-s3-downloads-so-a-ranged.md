---
id: BL-5873
title: Send a Range header for -r and -C - on s3:// downloads, so a ranged or resumed GET returns 206
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Operations.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Configuration.cs, Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpRequest.cs, Curl.Extended.Protocol.S3.UnitTests/Client/S3Operations, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Client.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3ProtocolHandler.cs, Curl.Extended.Protocol.S3.UnitTests/Client/S3RangeDownloadTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5873 — Send a Range header for -r and -C - on s3:// downloads, so a ranged or resumed GET returns 206

## Goal

`-r <range>` and `-C -` (and `-C <offset>`) on an `s3://` download send a signed `Range: bytes=...` header, so the server answers 206 with only the requested bytes, exactly as the same request over `https://...amazonaws.com` with `--aws-sigv4` does.

## Context

From Surly cookbook bug report `2026-11-24T1330-Curl-bug-report.md` (batch 14), finding 2 (Medium). Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11 (seen by the recipe writer; not re-run separately).

Repro: `surl --directory store --cert server.pem --key server.key --user AKIAIOSFODNN7EXAMPLE:s3cret s3://127.0.0.1:P/` with `store/media/big.bin` and a certificate naming `*.s3.us-east-1.amazonaws.com`; `curl -sS --cacert server.pem --connect-to ::127.0.0.1:P -u AKIAIOSFODNN7EXAMPLE:s3cret -r 0-9 -o part.bin -w "%{http_code} %{size_download}\n" s3://media/big.bin` prints `200` and the whole size; expected `206 10`. `-C -` on a partial file sends no `Range` either. Start in `Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Operations.cs` and `S3Configuration.cs` (carry the transfer's range/resume offset) and `Http/S3HttpRequest.cs` (write the header, and include it in the SigV4 signed headers as the https path does). A resumed download must append from the offset and report `size_download` as the bytes received, as over https.

## Acceptance criteria

- [x] `-r 0-9` on an `s3://` GET sends `Range: bytes=0-9`, and the 206 body (10 bytes) is written, pinned by a unit test against a test-double connection.
- [x] `-C -` with a partial output file sends `Range: bytes=<size>-` and appends; `-C <n>` sends `bytes=<n>-`, each pinned by a unit test.
- [x] A 200 reply to a ranged request behaves as curl's https path does (measure it; record it in Notes), pinned by a test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added S3Client.cs, S3ProtocolHandler.cs and a new test file to touches (the range threads through them; no other Doing task holds them). S3HttpRequest.cs and S3Configuration.cs needed no change: the range travels as a signed `range` header and the handler reads ResumeFrom and RangeText from the context.
- Behaviour copied from the https path (HttpRangeHeader, HttpDownloadConditions; read from code, not re-measured against a live server): `-r` text sent as `bytes=<text>`; a 200 reply to `-r` writes the whole body; `-C n` (n>0) sends `bytes=n-`; resume with 416 is success with 0 bytes; resume with 200 whose Content-Length equals n is 0 bytes, else exit 33; `-r` with 416 fails exit 22. Quality run clean (0 failing, 100/100).
- Linux/macOS CI: tests are platform-neutral.

## Log

- 2026-11-24: Created from Surly bug report 2026-11-24T1330, finding 2.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 944fda24 d8ed0a1f 575b2b3a 3d836d5e.
- 2026-11-24: Doing -> Done. s3:// downloads send a signed Range header for -r and -C; 416/200 resume handled as over https
