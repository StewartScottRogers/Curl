---
id: BL-2157
title: Rename the vendored upstream test cases from test<N>.rawhttp to test<N>.curltest
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary, Documentation/Planning/Decisions/ADR-0482-a-fix-found-through-an-upstream-case-also-gets-a-focused-unit-test.md]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2157 — Rename the vendored upstream test cases from test<N>.rawhttp to test<N>.curltest

## Goal

The 2,072 vendored upstream curl test cases in `Curl.Conformance.UnitTests/UpstreamTestData/`
are named `test<N>.curltest`, a name that says what they are, with their bytes unchanged.

## Context

Stewart, 2026-10-13. `.rawhttp` says something false: each file is a curl test case in
curl's XML-like `tests/data` format (server reply, command line, expected results). Many
cases are FTP, SMTP, IMAP, TFTP, file:// and others, not raw HTTP. That breaks CLAUDE.md's
"say what it does" rule.

Rejected: per-protocol extensions (`raw[PROTOCOL]`). A case can involve several protocols
(test2003: HTTP, FTP and TFTP), and the protocol lives in each case's `<server>` line, not in
upstream's file name. Rejected: `.xml`, because upstream's files are not always well-formed
XML and tools would try to parse them. The protocol labels and per-protocol test categories
belong to BL-2154, which depends on this task.

Where `.rawhttp` appears:
- `UpstreamTestData/Update-UpstreamTestData.ps1` (the `test*.rawhttp` filter and suffix)
- `UpstreamTestData/.gitattributes` (`test*.rawhttp -text`)
- `UpstreamTestData/README.md`
- `Curl.Conformance.UnitTests.csproj` (the `None Update` pattern)
- `UpstreamTestDataTests.cs`, `UpstreamCaseRunnerTests.cs`, `UpstreamConformanceTests.cs`,
  `UpstreamCaseScreeningTests.cs`, and wherever `Curl.Conformance.UnitLibrary` builds a case
  path

`Gap/` and `Audit/` do not read these files: `grep rawhttp` there finds nothing, and the gap
office reads upstream's tarball. Do the rename with `git mv`, so history follows the files.
Keep `-text` in `.gitattributes` for the new extension.

## Acceptance criteria

- [x] Every `UpstreamTestData/test*.rawhttp` is now `test*.curltest` with identical bytes, and no `.rawhttp` file remains.
- [x] `grep -ri rawhttp` over the repository finds nothing outside `Tasks/Done` and the git history.
- [x] `Update-UpstreamTestData.ps1 -Tag curl-8_22_0` writes `.curltest` files and leaves `git status` empty.
- [x] `.gitattributes`, the csproj, the README and the tests use `.curltest`, and `UpstreamTestDataTests` still asserts 2,072 cases.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green, the ratchet included.

## Notes

- 2026-10-13 (lane 9): `Curl.Conformance.UnitLibrary` never named the extension; only the test project did. To meet "grep finds nothing", ADR-0482 (added to `touches`; no task in Doing names it) and the Backlog tasks BL-2154 and BL-2155 now say "vendored upstream cases" instead of `.rawhttp` cases. `UpstreamTestDataTests` doc comments still said curl 8.21.0 after BL-2153; they now say 8.22.0. `Update-UpstreamTestData.ps1 -Tag curl-8_22_0` after the rename changed no file. The README now says why the extension is `.curltest`.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. 2,072 upstream cases renamed to test<N>.curltest with git mv, bytes unchanged; script, gitattributes, csproj, README and tests use .curltest; build and fast tests green.
