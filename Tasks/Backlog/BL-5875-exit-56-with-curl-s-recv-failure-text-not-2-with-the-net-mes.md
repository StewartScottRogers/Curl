---
id: BL-5875
title: Exit 56 with curl's recv-failure text, not 2 with the .NET message, when an s3:// connection drops mid-transfer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3TransferException.cs, Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpFailure.cs, Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs, Curl.Extended.Protocol.S3.UnitTests/Http/S3HttpFailure]
requirement: none
created: 2026-11-24
completed:
---
# BL-5875 — Exit 56 with curl's recv-failure text, not 2 with the .NET message, when an s3:// connection drops mid-transfer

## Goal

An `s3://` download whose connection drops mid-body exits 56 with curl's `Recv failure: Connection was reset` message, as the same drop over `https://` with `--aws-sigv4` does, not 2 with .NET's `...forcibly closed by the remote host` text.

## Context

From Surly cookbook bug report `2026-11-24T1330-Curl-bug-report.md` (batch 14), finding 4 (Low). Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11 (seen by the recipe writer; not re-run separately).

Repro: the S3 server from BL-5873 (`surl --directory store ... s3://127.0.0.1:P/`) plus `--fault "drop;after-bytes=1500000"`; `curl -sS --cacert server.pem --connect-to ::127.0.0.1:P -u AKIAIOSFODNN7EXAMPLE:s3cret -o big.bin s3://media/big.bin` exits 2 with the .NET text. Over `https://media.s3.us-east-1.amazonaws.com/big.bin` with `--aws-sigv4 aws:amz:us-east-1:s3` the same drop gives 56. Start in `Curl.Extended.Protocol.S3.UnitLibrary/Http/S3HttpConnectionReader.cs` and `S3HttpFailure.cs` / `Client/S3TransferException.cs`: catch the `IOException` from the body read and turn it into the recv-failure result, matching the https path's exit code and message (also a close before Content-Length is reached: curl's https path gives 18 there; match it). The OCI client has the same shape (`OciHttpConnectionReader`): check it and file a follow-up if it differs.

## Acceptance criteria

- [ ] A test-double connection that throws a reset mid-body makes the `s3://` GET exit 56 with curl's recv-failure message, pinned by a unit test.
- [ ] A connection that closes before `Content-Length` bytes exits as the https path does (18, `transfer closed with N bytes remaining to read`), pinned by a unit test.
- [ ] No .NET exception text reaches stderr for these cases.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from Surly bug report 2026-11-24T1330, finding 4.
