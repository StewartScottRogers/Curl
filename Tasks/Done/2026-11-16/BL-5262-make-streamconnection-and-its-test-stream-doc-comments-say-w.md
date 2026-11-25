---
id: BL-5262
title: Make StreamConnection and its test stream doc comments say what the code does after BL-5259
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/StreamConnection.cs, Curl.Networking.UnitTests/StreamConnectionTests.cs]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5262 — Make StreamConnection and its test stream doc comments say what the code does after BL-5259

## Goal

Every doc comment that BL-5259 (AF-0808) touched or displaced in `StreamConnection` and `StreamConnectionTests` says exactly what its member does.

## Context

Shift-end review of 717cc1483a..7877076cd1 (ADR-0710); found by the type-design-analyzer and code-reviewer.

- `Curl.Networking.UnitTests/StreamConnectionTests.cs:298`: the summary "A stream whose every read fails with the given exception." was left above the new `ReadOnceThenFailingStream`, which serves one byte and fails after that. `ReadFailingStream`, which the summary describes, now has none. The doc names the wrong type.
- `Curl.Networking.UnitLibrary/StreamConnection.cs:55-58`: the `HasUnreadBytesOrEndBeforeWrite` summary still says "the check run before each write". Since BL-5259, `WriteAsync` runs it only after the connection has read a byte; before that it runs `HasUnreadBytesBeforeFirstRead`.
- `StreamConnection.cs`, `ReadAsync` remarks (around lines 113-121): the BL-5259 edit left two unwrapped run-on lines. It says "a fresh connection never read from ignores the FIN" where the code means "does not count an unread FIN until it has read a byte".

CLAUDE.md's "say what it does, do what it says" rule treats a misaligned doc as a defect.

## Acceptance criteria

- [x] `ReadFailingStream` carries the summary "A stream whose every read fails with the given exception." (or an equivalent that is true of it).
- [x] `ReadOnceThenFailingStream` has its own summary saying its first read returns one byte and every later read fails with the given exception.
- [x] The `HasUnreadBytesOrEndBeforeWrite` summary says it runs before a write only once the connection has read a byte.
- [x] The `ReadAsync` remarks are wrapped like the rest of the file, and they state the first-write rule ("an unread FIN is not counted until the connection has read a byte") in a sentence of its own.
- [x] `dotnet build` is clean, and the fast tests of `Curl.Networking.UnitTests` pass.

## Notes

Doc-only. No behaviour change.

## Log

- 2026-11-15: Created by the shift-end review (ADR-0710).
- 2026-11-15: Backlog -> Doing. Criteria at claim: 33bca73c de0425ad 55101ad7 a487ed1b e3d1342b.
- 2026-11-15: Doing -> Done. Doc comments realigned; Networking fast tests green
