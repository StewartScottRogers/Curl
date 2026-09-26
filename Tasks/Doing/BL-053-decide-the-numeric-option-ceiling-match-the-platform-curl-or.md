---
id: BL-053
title: Decide the numeric option ceiling - match the platform curl or one ceiling everywhere
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-053 — Decide the numeric option ceiling

## Goal

Stewart decides the largest value Curl accepts for a numeric option, and the decision is
recorded as an ADR under `Documentation/Planning/Decisions/`.

## Context

BL-037 added `CommandLineNumber.ParseNonNegative` in `Curl.Cli.UnitLibrary`. It reads
into an `int` and refuses anything above `int.MaxValue` (2^31-1) with
`curl: option <spelled>: expected a proper numerical parameter`, which matches the local
curl 8.21.0 on Windows: `--tftp-blksize 99999999999` is refused that way.

Upstream curl reads these options into a C `long`. On Windows a `long` is 32 bits, so
Windows curl caps them at 2^31-1; on Linux and macOS a `long` is 64 bits, so curl there
accepts up to 2^63-1. Curl publishes native binaries for all three (BL-028), so the same
command line can be refused by one Curl build and accepted by another's upstream
counterpart - or not, depending on the choice made here.

Separately, `-C`/`--continue-at` (BL-027) reads a 64-bit offset (`curl_off_t`) on every
platform, so it must not reuse the `int` helper whichever way this is decided; BL-027
needs a 64-bit reader of its own.

The question: should Curl match the ceiling of the platform curl it replaces (2^31-1 on
Windows, 2^63-1 elsewhere), or pick one ceiling on every platform? Matching the platform
is what "drop-in replacement" implies. One ceiling everywhere is simpler but is a
deliberate divergence from upstream on at least one platform, which needs an ADR.

## Acceptance criteria

- [ ] Stewart's choice is recorded as the next free ADR under
      `Documentation/Planning/Decisions/`, stating the ceiling per platform, the upstream
      behaviour it matches or diverges from (curl 8.21.0), and that `--continue-at`
      reads a 64-bit value on every platform regardless.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] If the decision requires a code change to `CommandLineNumber`, a Claude task for
      it exists in `Tasks/Backlog` depending on this one.

## Notes

**Decision (Stewart, 2026-09-26):** Match the platform curl: 2^31-1 on Windows, 2^63-1 on Linux and macOS, as upstream's C `long` does. `--continue-at` reads a 64-bit value on every platform regardless.

## Log

- 2026-09-26: Created.
- 2026-09-26: Stewart decided: match the platform curl's ceiling. Reassigned to Claude to record the ADR.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Shift stopped while waiting for tokens (limit reset early); the run had not started
- 2026-09-26: Backlog -> Doing.
