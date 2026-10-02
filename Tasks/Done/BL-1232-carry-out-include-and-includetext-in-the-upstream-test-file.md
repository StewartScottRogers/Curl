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
completed: 2026-10-02
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

- [x] `%include` and `%includetext` are no longer in `UnsupportedMarkers`; a case using them is no longer skipped for it.
- [x] Tests in `Curl.Conformance.UnitTests` with an in-memory file seam pin: `%include %LOGDIR/a.txt%` followed by CRLF and by LF is replaced by the file's bytes with the line break consumed; `%includetext` turns the file's CRLF into LF and its result goes through variable substitution again (a `%TESTNUMBER` inside the file is replaced); `%include` runs after `%SP`-style macros and `%b64[]b64%`, so a macro inside the included bytes is not expanded; a missing file is included as nothing; the expander's other instructions behave as before.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- New `UpstreamTestFileInclusions` carries out both includes by hand (no `Regex`): every match of `%include(text)? ([^%]*)%[\n\r]+` left to right in one pass, so included bytes are not searched again; an include with no line break after it stays as written. `%includetext` turns CRLF into LF (Perl's `:crlf` layer leaves a lone CR) and the line is substituted again only when something was included.
- Seam: a `Func<string, byte[]?>` passed to a new four-argument `Expand` overload; the path reaches it decoded from UTF-8 (variables are inserted as UTF-8), and `null` means "cannot read", included as nothing. Chose a delegate over an interface: the expander is static and the runner is the only real caller. The three-argument overload keeps its old behaviour: includes left as written and listed as unsupported (now by `UpstreamTestFileInclusions.AddUnread`, not `UnsupportedMarkers`).
- `UpstreamCaseRunner` passes a static delegate reading the disk (`File.Exists` false -> nothing); a static field, not a method group, since a cached method-group delegate added a branch that took `RunAsync` past complexity 10.
- Every vendored case that uses an include today names `%SRCDIR/...`, which has no value in the harness, so those cases are still skipped, now for `%SRCDIR` rather than for the include.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green across the solution (Curl.Conformance.UnitTests 646 passed, 1805 skipped upstream cases); Measure-CodeQuality: Curl.Conformance.UnitLibrary 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. UpstreamTestFileExpander carries out %include and %includetext through an injected file reader; the case runner reads the disk
