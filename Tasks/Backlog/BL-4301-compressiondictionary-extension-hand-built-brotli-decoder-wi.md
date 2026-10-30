---
id: BL-4301
title: CompressionDictionary extension: hand-built Brotli decoder with a prefix dictionary for dcb
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4302]
touches: [Curl.Extension.CompressionDictionary.UnitLibrary/Brotli, Curl.Extension.CompressionDictionary.UnitTests/Brotli]
requirement: none
created: 2026-10-29
completed:
---
# BL-4301 — CompressionDictionary extension: hand-built Brotli decoder with a prefix dictionary for dcb

## Goal

`Curl.Extension.CompressionDictionary.UnitLibrary/Brotli` decodes a Brotli stream (RFC 7932) compressed against a prefix dictionary, as `dcb` (RFC 9842 section 4) needs.

## Context

Third part of BL-4132, split for size. The .NET 10 `BrotliDecoder` cannot attach a dictionary, so the decoder is built by hand here. It needs RFC 7932's static dictionary (122,784 bytes, RFC 7932 Appendix A) and transforms; commit the dictionary as an embedded resource and check its SHA-256 in a test. The prefix dictionary sits before the output as distance-reachable history (backward distances beyond the bytes written so far reach into it before the static dictionary), with a window of up to 16 MB (`lgwin` 24, RFC 9842 section 4). If the decoder alone is larger than one run, split it there (RFC 7932 core, then the prefix dictionary).

## Acceptance criteria

- [ ] A stream compressed with a prefix dictionary decodes to the original bytes, including distances that reach into the dictionary; unit tests use recorded bytes (from the reference `brotli` tool with `-D`, committed as test data) and cite RFC 7932 sections.
- [ ] Streams without a dictionary decode too, including static-dictionary references with transforms, metablock types and context modeling; a corrupt stream fails with a named exception; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Filed by BL-4132's first run when it split the task for size.

## Log

- 2026-10-29: Created.
