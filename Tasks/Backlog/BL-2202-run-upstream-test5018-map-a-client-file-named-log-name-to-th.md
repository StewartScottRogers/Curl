---
id: BL-2202
title: Run upstream test5018: map a <client><file> named log/<name> to the case's log directory
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2202 — Run upstream test5018: map a <client><file> named log/<name> to the case's log directory

## Goal

Upstream curl 8.22.0's test5018 runs in `Curl.Conformance.UnitTests` instead of being skipped.

## Context

Found by BL-2183. Test5018 writes `<client><file name="log/upload5018">` and uploads it with
`-T log/upload5018`: upstream's literal relative `log/`, not `%LOGDIR`. `UpstreamCaseRunner`
skips it ("`<client><file>` does not name a file by an absolute path"). `runtests.pl` runs curl
in the tests directory, where `log/` is the log directory, so the harness can map a leading
`log/` in a file name and in the command to the case's log directory, as
`UpstreamTestDirectoryComposition` already rewrites `%PWD/%LOGDIR`.

## Acceptance criteria

- [ ] An `UpstreamCaseRunnerTests` test pins that a `<client><file>` named `log/<name>`, and the command's `log/<name>`, are the case's log directory's `<name>`.
- [ ] Test5018's ratchet row is no longer skipped; if it passes, `5018` is in `PassingUpstreamCases.txt`, otherwise its first difference is under Notes.

## Notes

## Log

- 2026-10-13: Created.
