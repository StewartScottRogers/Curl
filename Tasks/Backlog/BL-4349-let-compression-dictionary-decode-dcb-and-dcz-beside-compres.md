---
id: BL-4349
title: Let --compression-dictionary decode dcb and dcz beside --compressed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs, Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryTransferRequest.cs, Curl.Extension.CompressionDictionary.UnitTests/CompressionDictionaryTransferHookTests.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4349 — Let --compression-dictionary decode dcb and dcz beside --compressed

## Goal

With `--compression-dictionary <dir>` and `--compressed` both given, Curl advertises `dcb, dcz` beside curl's own codings and decodes a `dcb` or `dcz` response instead of failing with exit 61.

## Context

ADR-1149 (BL-4132): the hook advertises nothing under `--compressed`, because curl's HTTP handler decodes content itself and refuses `dcb` and `dcz` (exit 61), and `Curl.Extension.Abstractions` gives a hook no way to run the handler with content decoding off. Add one (for example a `TransferContext` extension that hands curl's handler `HttpRequestOptions` with `Compressed` off), then let the hook send curl's `Accept-Encoding` value plus `dcb, dcz` and decode the other codings it receives the way curl would. RFC 9842 section 6.

## Acceptance criteria

- [ ] With both options and a matching dictionary, the request's `Accept-Encoding` is curl's `--compressed` value followed by `dcb, dcz`; unit test.
- [ ] A `dcb` or `dcz` response is decoded and a `gzip` one still is, as with `--compressed` alone; unit tests.
- [ ] ADR-1149 is updated to say so.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-30: Created.
