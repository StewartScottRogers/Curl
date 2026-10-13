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
completed: 2026-10-13
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

- [x] `certs/` matches upstream curl 8.22.0's `tests/certs` file set (build files excluded), byte for byte. Files added, removed or changed since 8.21.0 are listed in this task's Notes.
- [x] `Update-UpstreamTestData.ps1 -Tag curl-8_22_0` refreshes `certs/` too. Run again with the pinned tag, it leaves `git status` empty. Its help says so.
- [x] The README names 8.22.0 for the certs and describes the refresh correctly.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- Delivered directly rather than through the full `/feature` stages: the work is a byte-for-byte
  vendor refresh plus a script and README change, with no production code to plan, test or review.
- Source: the `curl-8_22_0` tag's GitHub archive (what `Update-UpstreamTestData.ps1` downloads),
  checked against the release tarball `curl-8.22.0.tar.xz`: the same 10 non-build files, same
  SHA-256 for each. The script leaves out `Makefile.*`, `CMakeLists.txt` and dot files.
- Changes since 8.21.0:
  - Removed: `srp-verifier-conf`, `srp-verifier-db` (no longer in upstream `tests/certs`; nothing in Curl referenced them).
  - Changed: `genserv.pl` (the openssl-missing message wording, and it prints the PATH searched).
  - Added: none. The `test-*.prm` files and `test-ca.cnf` are unchanged.
- A second run of `Update-UpstreamTestData.ps1 -Tag curl-8_22_0` left no unstaged changes.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. certs/ is curl 8.22.0's tests/certs byte for byte, and Update-UpstreamTestData.ps1 now refreshes it
