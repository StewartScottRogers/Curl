---
id: BL-2153
title: Re-vendor the upstream tests/certs files at curl 8.22.0 and correct the UpstreamTestData README
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2153 — Re-vendor the upstream tests/certs files at curl 8.22.0 and correct the UpstreamTestData README

## Goal

`Curl.Conformance.UnitTests/UpstreamTestData/certs/` holds the `tests/certs` files of curl
8.22.0, and the folder's README names 8.22.0 for both the test cases and the certs.

## Context

BL-2126 re-vendored the `test*.rawhttp` cases at `curl-8_22_0` (ADR-0479). The README's
`certs` section still says the files come from the curl 8.21.0 tarball (BL-1922), so either
the certs are a release behind or the README is wrong. Stewart asked for this on 2026-10-13.

Start at `Curl.Conformance.UnitTests/UpstreamTestData/README.md` (the `certs` section and the
refresh instructions) and `Update-UpstreamTestData.ps1`, which fetches only `tests/data`.
Source: the curl 8.22.0 release tarball (`https://curl.se/download/curl-8.22.0.tar.xz`), or
the `curl-8_22_0` tag's `tests/certs`. Use the same file set as before: everything except
build files (`Makefile.*`, `CMakeLists.txt`). Copy files byte for byte, and keep any
`.gitattributes` rule that protects their bytes.

Generating the certificate files themselves is BL-1923, not this task.

## Acceptance criteria

- [ ] `certs/` matches upstream curl 8.22.0's `tests/certs` file set (build files excluded), byte for byte. Files added, removed or changed since 8.21.0 are listed in this task's Notes.
- [ ] `Update-UpstreamTestData.ps1 -Tag curl-8_22_0` refreshes `certs/` too. Run again with the pinned tag, it leaves `git status` empty. Its help says so.
- [ ] The README names 8.22.0 for the certs and describes the refresh correctly.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-13: Created.
