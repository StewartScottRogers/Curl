---
id: BL-4948
title: Make the --content-digest one-line help say -d data, not uploads
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Extension.ContentDigest.UnitLibrary/ContentDigestExtension.cs, Curl.Extension.ContentDigest.UnitTests/ContentDigestExtensionTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4948 — Make the --content-digest one-line help say -d data, not uploads

## Goal

The `--extended-help` line for `--content-digest` matches ADR-1094: digests are sent for `-d` data.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-026 (Low, confirmed: reproduced at least twice).

- Repro: `curl -sS --content-digest -T somefile http://127.0.0.1:18800/upload` against a header logger; `curl --extended-help | grep content-digest`.
- Expected: `--extended-help` says "Sends SHA-256 Content-Digest and Repr-Digest fields with uploads".
- Actual: `-T` gets only `Want-*` headers; ADR-1094 and the per-option help say `-d` only, so the one-liner is the wrong part.
- Where to start: The content-digest entry in `ExtensionDescriptionCatalogue` (find its file and narrow touches when claiming).

## Acceptance criteria

- [x] The `--extended-help` line for `--content-digest` names `-d` data, not uploads.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Widened touches to Curl.Extension.ContentDigest.UnitLibrary/ContentDigestExtension.cs and its test: the description lives there, not in Composition; no other Doing task names it. Summary now says "with -d data"; unit test added.

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 3bc72e0f a0b49a29.
- 2026-11-11: Doing -> Done. Extended-help summary for --content-digest now says -d data; unit test added
