---
id: BL-1849
title: Close GF-0016 test3036: -OJ --no-clobber --retry exits 23 as curl 8.21.0 does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1849 — Close GF-0016 test3036: -OJ --no-clobber --retry exits 23 as curl 8.21.0 does

## Goal

Curl behaves as curl 8.21.0 does for upstream test3036 (`--no-clobber --output-dir ... -OJ --retry 1 --retry-all-errors`), so a later gap analysis measures `behaviour:test3036` of GF-0016 as `match`.

## Context

- Split from BL-1809, which closed test1642 and test1643 (`-J -L` names the file after the last `Location`).
- Interactive only (`lane: no`): the upstream test case sits in the gap analysis office's upstream cache, which the audit guard refuses to dark factory lanes, so a lane cannot read what the test sends. The reproduce command is in GF-0016.
- Finding evidence: the reference curl exits 23 with 0 bytes on stdout; Curl's stderr differs. Suggestion: with `--no-clobber` and `--retry`, fail on an existing file with exit 23 and the reference's message.
- Start at `RemoteHeaderNameStream.TryOpenAsync` (an output already open when a `Content-Disposition` arrives fails with exit 23 and no warning) and `DeferredOutputFileStream.TryOpenUnderNameAsync` (`--no-clobber` numbering); check what a `--retry` attempt does to the `-J` file the attempt before it opened.

## Acceptance criteria

- [ ] `behaviour:test3036`: Curl answers what curl 8.21.0 answers (exit 23, stdout 0 bytes, the same stderr), pinned by a `CurlCommandRunner` unit test.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-09: Backlog -> Doing.
