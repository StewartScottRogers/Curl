---
id: BL-5875
title: Exit 56 with curl's recv-failure text, not 2 with the .NET message, when an s3:// connection drops mid-transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3TransferException.cs, Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpFailure.cs, Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs, Curl.Extended.Protocol.S3.UnitTests/Http/S3HttpFailure, Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpException.cs, Curl.Extended.Protocol.S3.UnitLibrary/Http/S3ContentLengthBodyStream.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3ProtocolHandler.cs, Curl.Extended.Protocol.S3.UnitTests/Client, Curl.Extended.Protocol.S3.UnitTests/Http]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5875 — Exit 56 with curl's recv-failure text, not 2 with the .NET message, when an s3:// connection drops mid-transfer

## Goal

An `s3://` download whose connection drops mid-body exits 56 with curl's `Recv failure: Connection was reset` message, as the same drop over `https://` with `--aws-sigv4` does, not 2 with .NET's `...forcibly closed by the remote host` text.

## Context

From Surly cookbook bug report `2026-11-24T1330-Curl-bug-report.md` (batch 14), finding 4 (Low). Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11 (seen by the recipe writer; not re-run separately).

Repro: the S3 server from BL-5873 (`surl --directory store ... s3://127.0.0.1:P/`) plus `--fault "drop;after-bytes=1500000"`; `curl -sS --cacert server.pem --connect-to ::127.0.0.1:P -u AKIAIOSFODNN7EXAMPLE:s3cret -o big.bin s3://media/big.bin` exits 2 with the .NET text. Over `https://media.s3.us-east-1.amazonaws.com/big.bin` with `--aws-sigv4 aws:amz:us-east-1:s3` the same drop gives 56. Start in `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs` and `S3HttpFailure.cs` / `Client/S3TransferException.cs`: catch the `IOException` from the body read and turn it into the recv-failure result, matching the https path's exit code and message (also a close before Content-Length is reached: curl's https path gives 18 there; match it). The OCI client has the same shape (`OciHttpConnectionReader`): check it and file a follow-up if it differs.

## Acceptance criteria

- [x] A test-double connection that throws a reset mid-body makes the `s3://` GET exit 56 with curl's recv-failure message, pinned by a unit test.
- [x] A connection that closes before `Content-Length` bytes exits as the https path does (18, `transfer closed with N bytes remaining to read`), pinned by a unit test.
- [x] No .NET exception text reaches stderr for these cases.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decided: S3HttpConnectionReader turns an IOException into S3HttpFailure.ReceiveFailed ("Recv failure: Connection was reset", exit 56); a Content-Length body cut short is the new S3HttpFailure.BodyShort ("transfer closed with N bytes remaining to read", exit 18); S3ProtocolHandler maps them. Other failures stay exit 8. touches widened to the handler, exception and test folders (no other lane holds them).
- OCI client: OciHttpClient already catches IOException; not re-measured, no follow-up filed.
- Quality run: 0 failing members; criteria 3 (no .NET text) holds as the mapped message replaces the IOException text.

## Log

- 2026-11-24: Created from Surly bug report 2026-11-24T1330, finding 4.
- 2026-11-24: Backlog -> Doing. Criteria at claim: a28d6029 656cbfba 36a6d25e 3d836d5e.
- 2026-11-24: Doing -> Done. s3:// mid-body reset exits 56 with Recv failure text; short Content-Length exits 18
