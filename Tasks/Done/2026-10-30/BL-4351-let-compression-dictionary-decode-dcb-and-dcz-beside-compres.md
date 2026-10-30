---
id: BL-4351
title: Let --compression-dictionary decode dcb and dcz beside --compressed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.Abstractions.UnitTests/Transfer/TransferContextContentDecodingTests.cs, Curl.Extension.CompressionDictionary.UnitLibrary/CompressedResponseBody.cs, Curl.Extension.CompressionDictionary.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-1154-compression-dictionary-stores-and-advertises-rfc-9842-dictionaries-in-a-transfer-hook.md, Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs, Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryTransferRequest.cs, Curl.Extension.CompressionDictionary.UnitTests/CompressionDictionaryTransferHookTests.cs]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4351 — Let --compression-dictionary decode dcb and dcz beside --compressed

## Goal

With `--compression-dictionary <dir>` and `--compressed` both given, Curl advertises `dcb, dcz` beside curl's own codings and decodes a `dcb` or `dcz` response instead of failing with exit 61.

## Context

ADR-1149 (BL-4132): the hook advertises nothing under `--compressed`, because curl's HTTP handler decodes content itself and refuses `dcb` and `dcz` (exit 61), and `Curl.Extension.Abstractions` gives a hook no way to run the handler with content decoding off. Add one (for example a `TransferContext` extension that hands curl's handler `HttpRequestOptions` with `Compressed` off), then let the hook send curl's `Accept-Encoding` value plus `dcb, dcz` and decode the other codings it receives the way curl would. RFC 9842 section 6.

## Acceptance criteria

- [x] With both options and a matching dictionary, the request's `Accept-Encoding` is curl's `--compressed` value followed by `dcb, dcz`; unit test.
- [x] A `dcb` or `dcz` response is decoded and a `gzip` one still is, as with `--compressed` alone; unit tests.
- [x] ADR-1149 is updated to say so.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- "ADR-1149" in Context and the criteria is the compression dictionary ADR as numbered before the rebase resolver
  moved it; it is now ADR-1154 (ADR-1149 is the gRPC reflection ADR). ADR-1154's Decision and Left out sections are
  updated.
- Added `TransferContextContentDecoding.WithContentDecodingOff` to `Curl.Extension.Abstractions`: it hands curl's
  handler `HttpRequestOptions` with `Compressed` off. With `--compressed` and a matching dictionary the hook sends
  `Accept-Encoding: <curl's AcceptEncoding>, dcb, dcz` and decodes every coding of the response's `Content-Encoding`
  fields itself through the new `CompressedResponseBody` (dcb and dcz against the dictionary; gzip, x-gzip, deflate as
  zlib or raw, br through the BCL; zstd through Curl.Zstandard). Without a matching dictionary curl's handler decodes
  as before.
- Decided: `--raw` (`Compressed` with `PassesContentCoding`) advertises nothing, since it asks for the body as
  encoded. The exit 61 text, the 8 MiB zstd window and `--max-filesize` on the encoded size differ from curl only on
  bodies that fail or exceed a limit; recorded in ADR-1154's Left out.
- `touches` widened (no task in Doing on origin/work/dark-factory names them): the new `CompressedResponseBody.cs`
  and `TransferContextContentDecodingTests.cs`, the extension's CLAUDE.md, and ADR-1154.
- Measure-CodeQuality: Curl.Extension.CompressionDictionary.UnitLibrary 100% line and branch, 0 failing members.
  Curl.Extension.Abstractions.UnitLibrary: the only failing member is `CertificateCheckHooks.CheckedAsync`, which
  BL-4358 (in Doing) is fixing; the new file is fully covered.
- Fast tests green on Windows. The new tests use no paths, OS text or certificates, so they are platform-neutral.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: e6421b98 6b6a8222 4d3f4f7d 478f867b 3d836d5e.
- 2026-10-30: Doing -> Done. --compression-dictionary advertises dcb, dcz after curl's --compressed codings and decodes dcb, dcz and curl's own codings itself
