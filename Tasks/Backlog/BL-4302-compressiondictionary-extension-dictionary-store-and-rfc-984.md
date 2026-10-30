---
id: BL-4302
title: CompressionDictionary extension: dictionary store and RFC 9842 header and framing wire format
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.CompressionDictionary.UnitLibrary, Curl.Extension.CompressionDictionary.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4302 — CompressionDictionary extension: dictionary store and RFC 9842 header and framing wire format

## Goal

`Curl.Extension.CompressionDictionary.UnitLibrary` exists with the RFC 9842 wire format: parsing `Use-As-Dictionary`, building `Available-Dictionary` and `Dictionary-ID`, matching a request URL against a dictionary's `match` pattern, reading the `dcb` and `dcz` stream headers, and a dictionary store that keeps dictionaries and picks the best match.

## Context

First part of BL-4132 (Use Compression Dictionary Transport), split for size. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Specification: RFC 9842. Never reference a `Curl.Protocol.*` library or another extension. No option, hook or catalogue line here: BL-4132 adds those once the decoders (BL-4300, BL-4301) exist.

Start by scaffolding the two projects with the `new-project` skill, in `/Extensions/CompressionDictionary/` in `Curl.slnx` (one single-line `<Folder>` under the marker comment), modelled on `Curl.Extension.HttpCache.UnitLibrary`.

## Acceptance criteria

- [ ] `Curl.Extension.CompressionDictionary.UnitLibrary` and `.UnitTests` exist in `/Extensions/CompressionDictionary/` in `Curl.slnx`, and the build's `VerifyExtensionIsolation` passes.
- [ ] `Use-As-Dictionary` is parsed as an RFC 8941 dictionary with `match`, `match-dest`, `id` and `type` (RFC 9842 section 2.1), ignoring a header without `match` or with a `type` other than `raw`; unit tests cite the section.
- [ ] `Available-Dictionary` is built as the RFC 8941 byte sequence of the dictionary's SHA-256 and `Dictionary-ID` as an sf-string (RFC 9842 sections 2.2 and 2.3); unit tests.
- [ ] A request URL is matched against a `match` pattern resolved against the dictionary's URL, same-origin only (RFC 9842 section 2.1.1 and 2.2.2, the URLPattern subset the RFC needs); unit tests.
- [ ] The `dcb` header (magic `FF 44 43 42` and 32-byte SHA-256) and `dcz` header (magic `5E 2A 4D 18 20 00 00 00` and 32-byte SHA-256) are read and checked against the advertised dictionary (RFC 9842 sections 4 and 5); unit tests.
- [ ] A dictionary store keeps dictionaries (in a directory given by path, files in temporary directories in tests) and picks the longest-match dictionary for a URL (RFC 9842 section 2.2.2); unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

Filed by BL-4132's first run when it split the task for size.

## Log

- 2026-10-29: Created.
