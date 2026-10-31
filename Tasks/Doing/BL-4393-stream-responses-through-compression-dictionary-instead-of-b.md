---
id: BL-4393
title: Stream responses through --compression-dictionary instead of buffering the whole body
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs, Curl.Extension.CompressionDictionary.UnitTests/CompressionDictionaryTransferHookTests.cs]
requirement: none
created: 2026-10-31
completed:
---
# BL-4393 — Stream responses through --compression-dictionary instead of buffering the whole body

## Goal

With --compression-dictionary, a response without Use-As-Dictionary streams to the output as it arrives, as it does without the extension; only a response stored as a dictionary is held.

## Context

Shift-end review 2026-10-31 (type-design-analyzer). Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs:29-41 always swaps the output for a MemoryStream and writes it only after next returns. Under -N a server-sent-events stream shows nothing until the server closes, and a multi-gigabyte download is held entirely in memory. Also line 42 stores the dictionary under the requested URL, not Report.EffectiveUrl, so after -L a dictionary from origin B is advertised for origin A (RFC 9842 section 2.1); fix it in the same change.

## Acceptance criteria

- [ ] A test in CompressionDictionaryTransferHookTests shows bytes of a response without Use-As-Dictionary reach the output before the response ends.
- [ ] A test shows a dictionary received after a redirect is stored under the effective URL.
- [ ] dotnet build is clean, the fast tests pass and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 2e78e4b1 0e7256db f874b647.
