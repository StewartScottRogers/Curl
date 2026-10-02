---
id: BL-1232
title: Carry out %include and %includetext in the upstream test-file expander
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1232 — Carry out %include and %includetext in the upstream test-file expander

## Goal

`UpstreamTestFileExpander` replaces a line's `%include <path>%` and `%includetext <path>%` with the named file's contents exactly as curl 8.21.0's `runtests.pl` does, reading files through an injected seam, so upstream cases that use them are run instead of skipped as `the harness does not carry out %include`.

## Context

- Today `Curl.Conformance.UnitLibrary/UpstreamTestInstructions.cs` lists `%include ` and `%includetext ` among `UnsupportedMarkers` (lines 23-30); `UpstreamCaseScreening.UnsupportedInstruction` then skips the case. `UpstreamTestFileExpander.Expand(ReadOnlySpan<byte> file, IReadOnlyDictionary<string, string> variables, IReadOnlySet<string> features)` (line 35) works on bytes and reads no file; its only caller is `UpstreamCaseRunner` (line 71).
- curl 8.21.0, `tests/runner.pm` `prepro` (lines ~355-385 at `curl-8_21_0`) processes each kept line in this order: `subvariables`; `subtextfile`, and when it replaced anything, `subvariables` again; `subchars`; `subbase64`; `subsha256base64file`; `substrippemfile`; then the `crlf` newline handling for `<data>`/`<connect>` parts. In `tests/testutil.pm`: `subtextfile` (lines 113-119) replaces `%includetext ([^%]*)%[\n\r]+` with the file read through Perl's `:crlf` layer (CRLF becomes LF); `subbase64` ends (line 173) by replacing `%include ([^%]*)%[\n\r]+` with the file's raw bytes (`includefile`, lines 102-111). Both consume the line break after the closing `%`. A file that cannot be opened is included as nothing (`open` fails quietly and the read is empty).
- Read files through a seam the tests can fake (an interface or delegate taking the path after substitution and returning bytes, or nothing), keep the existing `Expand` overload's behaviour for callers that pass none, and make `UpstreamCaseRunner` pass a real one; base class library only.
- See `Curl.Conformance.UnitLibrary/CLAUDE.md` for the library's rules (no `Regex`: tag lines are recognised by hand so source-generated code does not count against coverage).

## Acceptance criteria

- [ ] `%include` and `%includetext` are no longer in `UnsupportedMarkers`; a case using them is no longer skipped for it.
- [ ] Tests in `Curl.Conformance.UnitTests` with an in-memory file seam pin: `%include %LOGDIR/a.txt%` followed by CRLF and by LF is replaced by the file's bytes with the line break consumed; `%includetext` turns the file's CRLF into LF and its result goes through variable substitution again (a `%TESTNUMBER` inside the file is replaced); `%include` runs after `%SP`-style macros and `%b64[]b64%`, so a macro inside the included bytes is not expanded; a missing file is included as nothing; the expander's other instructions behave as before.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
