---
id: BL-143
title: Vendor curl 8.21.0's tests/data cases into Curl.Conformance.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-005, BL-148]
touches: [Curl.Conformance.UnitTests/UpstreamTestData, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj, Curl.Conformance.UnitTests/UpstreamTestDataTests.cs, Curl.Conformance.UnitTests/CLAUDE.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
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

- [x] `Curl.Conformance.UnitTests/UpstreamTestData/` holds every `test*` file from `tests/data`
      at `curl-8_21_0`, byte for byte (a `.gitattributes` in the folder marks the files `-text`
      so line endings are kept), and the count is recorded in `Notes`.
- [x] `Curl.Conformance.UnitTests/UpstreamTestData/COPYING` is curl's `COPYING` at the same tag,
      and `UpstreamTestData/README.md` names the tag and how to refresh.
- [x] `Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1 -Tag curl-8_21_0`
      reproduces the folder with an empty `git status` for it.
- [x] The files are copied to the test output directory, and a test asserts the vendored count.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Vendored count: **2013** `test*` files from `tests/data` at `curl-8_21_0`. Every file's git
  blob hash was compared against GitHub's tree for `curl-8_21_0:tests/data`: all 2013 identical.
- Source archive: the script downloads GitHub's tag archive (`archive/refs/tags/<Tag>.tar.gz`)
  rather than the curl.se release tarball, because the tag archive is exactly the git tree at the
  tag and the tarball only carries what `EXTRA_DIST` lists. Default taken under rule 1.
- The script calls Windows' `System32	ar.exe` (bsdtar): Git Bash's GNU tar, when first on
  PATH, reads `C:...` as a remote host and fails.
- `touches` widened: the count test needs a new file, `UpstreamTestDataTests.cs`, and the
  project `CLAUDE.md` lists the project's tests. No task in `Doing` touches
  `Curl.Conformance.UnitTests`, so both were added.
- The count test reads files copied to the test output and runs in the fast suite, not under
  `Integration`: ADR-0013 puts the vendored-data conformance run in the fast suite, and the
  data is part of the build output, not external state.
- Verified: `dotnet build` 0 warnings, 0 errors; fast tests green (Curl.Conformance.UnitTests
  155 passed); rerunning the script with `-Tag curl-8_21_0` left `git status` empty for the folder.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. curl 8.21.0's 2013 tests/data files and COPYING are vendored, copied to test output, count-tested, and refreshable by tag
