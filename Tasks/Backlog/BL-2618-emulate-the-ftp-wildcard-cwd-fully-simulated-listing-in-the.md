---
id: BL-2618
title: Emulate the FTP wildcard CWD fully_simulated listing in the case runner so cases 574, 575, 1113, 1162 and 1163 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2618 — Emulate the FTP wildcard CWD fully_simulated listing in the case runner so cases 574, 575, 1113, 1162 and 1163 run

## Goal

The FTP stand-in in the upstream case runner carries out `CWD fully_simulated` and its wildcard listing, so cases 574, 575, 1113, 1162 and 1163 run instead of being skipped.

## Context

Found by BL-2532, which ported lib574 and lib575. `UpstreamCaseScreening.FtpDataConnectionCommand` skips any case whose protocol holds `CWD fully_simulated`; upstream's `tests/ftpserver.pl` (curl 8.22.0) answers it with a fixed UNIX LIST and files. BL-1906 (Deferred) noted it as not emulated.

## Acceptance criteria

- [ ] Each case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
