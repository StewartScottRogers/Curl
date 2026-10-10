---
id: BL-2012
title: Expand Perl escapes such as \x00 in upstream REPLY lines when measuring upstream cases so test2108 measures match
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [*]
lane: no
requirement: none
created: 2026-10-10
completed:
---
# BL-2012 — Expand Perl escapes such as \x00 in upstream REPLY lines when measuring upstream cases so test2108 measures match

## Goal

The gap analysis office's upstream-case runner (`Measure-UpstreamCases.cs` in the gap office's tools) serves a `REPLY` line from an upstream `<servercmd>` the way upstream's `tests/ftpserver.pl` does, expanding Perl double-quote escapes such as `\x00`, so `behaviour:test2108` measures `match`.

## Context

- Found by BL-1983 (GF-0053). Upstream curl 8.21.0 `tests/data/test2108` holds `REPLY PASS 230 logged\x00 in`; `ftpserver.pl` evaluates the reply text as a Perl `qq{}` string, so the server sends a real NUL byte and curl ends with exit 8 after `PASS`.
- Curl already does the same for a real NUL (BL-1117), pinned for this exact exchange by `FtpProtocolHandlerNulByteReplyTests.ExecuteAsync_NulByteInTheReplyToPass_FailsWithExit8AfterPassAndSendsNoPwd`. The gap measured Curl sending `PWD`, so the runner most likely sends the four characters `\x00` literally.
- The runner is an audit path (ADR-0433), so this task is interactive only (`lane: no`); a lane's guard refused even naming its folder, hence `touches: [*]`. An interactive session should narrow `touches` to the runner's file.

## Acceptance criteria

- [ ] The runner expands at least `\xHH`, `\r`, `\n`, `\t` and `\\` in `REPLY` text, as `ftpserver.pl`'s `qq{}` evaluation does.
- [ ] Re-measuring upstream case 2108 with the runner reports `match`.

## Notes

## Log

- 2026-10-10: Created.
