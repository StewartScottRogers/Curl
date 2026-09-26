---
id: BL-143
title: Vendor curl 8.21.0's tests/data cases into Curl.Conformance.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-005, BL-148]
touches: [Curl.Conformance.UnitTests/UpstreamTestData, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj]
requirement: none
created: 2026-09-26
completed:
---
# BL-143 — Vendor curl 8.21.0's tests/data cases into Curl.Conformance.UnitTests

## Goal

The `tests/data/test*` files from curl release tag `curl-8_21_0` sit in
`Curl.Conformance.UnitTests/UpstreamTestData/`, copied to the test output, with a script that
refreshes them to a named tag.

## Context

- ADR-0013, decision 3: vendored and pinned; tests never download anything.
- Upstream: https://github.com/curl/curl/tree/curl-8_21_0/tests/data. The release tarball
  (https://curl.se/download/curl-8.21.0.tar.gz) holds the same folder.
- curl is under the curl licence (https://curl.se/docs/copyright.html); its `COPYING` file goes
  beside the data so the notice travels with it.
- The script is PowerShell (`Invoke-WebRequest` and `tar`), takes `-Tag`, and replaces the
  folder's `test*` files wholesale.

## Acceptance criteria

- [ ] `Curl.Conformance.UnitTests/UpstreamTestData/` holds every `test*` file from `tests/data`
      at `curl-8_21_0`, byte for byte (a `.gitattributes` in the folder marks the files `-text`
      so line endings are kept), and the count is recorded in `Notes`.
- [ ] `Curl.Conformance.UnitTests/UpstreamTestData/COPYING` is curl's `COPYING` at the same tag,
      and `UpstreamTestData/README.md` names the tag and how to refresh.
- [ ] `Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1 -Tag curl-8_21_0`
      reproduces the folder with an empty `git status` for it.
- [ ] The files are copied to the test output directory, and a test asserts the vendored count.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
