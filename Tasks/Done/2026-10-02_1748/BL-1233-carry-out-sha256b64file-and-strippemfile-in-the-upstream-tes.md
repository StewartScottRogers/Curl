---
id: BL-1233
title: Carry out %sha256b64file and %strippemfile in the upstream test-file expander
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1232]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1233 — Carry out %sha256b64file and %strippemfile in the upstream test-file expander

## Goal

`UpstreamTestFileExpander` replaces `%sha256b64file[<path>]sha256b64file%` with the base64 of the named file's SHA-256 and `%strippemfile[<path>]strippemfile%` with the file's PEM bodies, as curl 8.21.0's `tests/testutil.pm` does, through the file seam BL-1232 adds, so the upstream TLS pinning cases that use them are no longer skipped for it.

## Context

- Today both markers are in `Curl.Conformance.UnitLibrary/UpstreamTestInstructions.cs` `UnsupportedMarkers` (lines 28-29), so `UpstreamCaseScreening` skips any case that uses them. BL-1232 gives the expander a seam for reading files; use the same one.
- curl 8.21.0, `tests/testutil.pm` at `curl-8_21_0`:
  - `subsha256base64file` (lines 249-258): for each `%sha256b64file[(.*?)]sha256b64file%`, matched ignoring case, the path has its `%XX` escapes decoded, the file is read raw (`<:raw`) and the marker becomes `encode_base64(sha256(bytes), "")` (`get_sha256_base64`, lines 244-247: standard base64, no line breaks).
  - `substrippemfile` (lines 270-279): for each `%strippemfile[(.*?)]strippemfile%`, the path is `%XX`-decoded and the marker becomes `get_file_content` (lines 261-268): the file with everything outside `-----BEGIN ...-----` / `-----END ...-----` lines removed by `s/(^|-----END .*?-----[\r\n]?)(.*?)(-----BEGIN .*?-----|$)/$1$3/gs`, CRLF turned into LF and one trailing newline removed (`chomp`).
  - `tests/runner.pm` `prepro` runs them after `subbase64` (so after `%include`) and before the `crlf` handling.
- SHA-256 is in the BCL (`System.Security.Cryptography.SHA256`); no package.

## Acceptance criteria

- [x] Neither marker is in `UnsupportedMarkers`; a case using them is no longer skipped for it.
- [x] Tests in `Curl.Conformance.UnitTests` with the in-memory file seam pin: `%sha256b64file[%LOGDIR/k.pub]sha256b64file%` (and the upper-case spelling) gives the base64 SHA-256 of the file's bytes; a `%2F` in the path is decoded before the read; `%strippemfile[...]strippemfile%` over a file with text before, between and after two PEM blocks gives exactly what `get_file_content`'s three steps give, worked out by hand in the test's comment, once with LF and once with CRLF line ends (note that `[\r\n]?` keeps only the `\r` of a CRLF after an END line, and the later CRLF-to-LF step leaves that lone `\r`); two markers on one line are both replaced.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- New `UpstreamTestFileContentInstructions` carries out both markers after `%include`, through the same `readFile` delegate BL-1232 added; `UpstreamTestInstructions.ReplaceEach` and `DecodePercentPairs` became internal-public so it reuses them. `get_file_content`'s substitution runs as an interpreted `Regex` (Singleline; .NET's `$` matches before a final LF as Perl's does), the way `UpstreamRegex` already uses one.
- Default taken: a file that cannot be read counts as empty (SHA-256 of no bytes; strippem gives nothing), matching how includes treat a missing file. Without a reader both markers are still listed as unsupported, like the includes.
- Coverage: Curl.Conformance.UnitLibrary 100% line, 100% branch, worst CRAP 10.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The upstream test-file expander carries out %sha256b64file and %strippemfile through its file reader
