---
id: BL-5120
title: Count bytes decoded before the error in %{size_download} with --tr-encoding
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Original.Protocol.Http.UnitTests/TransferEncodingPartialDecodeSizeDownloadTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5120 — Count bytes decoded before the error in %{size_download} with --tr-encoding

## Goal

With `--tr-encoding`, when a transfer-coded body fails to decode partway, `%{size_download}` counts the bytes decoded and delivered before the error (as upstream curl does), instead of 0.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-070 (Low, Confirmed twice each). Area: HTTP `--tr-encoding`, `-w %{size_download}`.

**Repro.**
1. Run a raw server on 127.0.0.1:21001 (PowerShell or a C# file-based app, not Python). For `/raw/te_gztrail` it sends `HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n`, one chunk holding gzip(`"hello world " x 50`) followed by the bytes `TRAILINGJUNK`, then `0\r\n\r\n`. For `/raw/te_gz2members` the chunk holds gzip(first 100 bytes of B) + gzip(rest of B) with `B = "hello world " x 50` (600 bytes).
2. `curl -sS --tr-encoding -o out -w "%{http_code} %{size_download}\n" http://127.0.0.1:21001/raw/te_gztrail`, then the same for `/raw/te_gz2members`.

**Expected (upstream curl).** `200 600` with `curl: (23) Failed reading the chunked-encoded stream`; `200 100` with `curl: (23) Multi-member gzip response not supported`.

**Actual.** Same stderr, exit codes and `out` contents, but `200 0` in both. Related wording difference: a corrupt `Transfer-Encoding: zstd, chunked` body gives exit 61 in both, but the port says `Unrecognized or bad HTTP Content or Transfer-Encoding` where upstream says `Failed reading the chunked-encoded stream`; fix that wording in the same change if it is a one-line mapping, otherwise record it in the task's Log as a follow-up.

**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs`, `CopyAsync` / `DecoderFor` (lines 310-395): when the transfer-coding decoder throws, the decoded byte count is not kept in `BytesWritten`.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests/TransferEncodingPartialDecodeSizeDownloadTests.cs` prove that for a gzip-plus-trailing-junk transfer-coded body the failure is exit 23 and the reported downloaded size is 600, and for a two-member gzip body it is exit 23 with size 100.
- [ ] The delivered output bytes in both cases are the decoded bytes before the error, unchanged from today.
- [ ] A regression test proves a body that fails before any byte is decoded still reports size 0, and a successful `--tr-encoding` transfer's size is unchanged.
- [ ] Tests are platform-neutral, using in-memory connection stand-ins.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the two commands print `200 600` and `200 100`.

## Log

- 2026-11-13: Created.
