# ADR-0423 — The conformance harness caps one `%repeat` at 16 MiB and lists a longer one as unsupported

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1648
- Decided by Claude under Stewart's delegation.

## Context

`UpstreamTestFileExpander.Expand` replaces `%repeat[N x content]%` with `content` N times, as
upstream's `testutil.pm` (`subbase64`, curl-8_21_0) does, for any N up to `int.MaxValue`.
`%repeat[2000000000 x ab]%` asks for four billion characters, past the CLR's string limit, so
the expander, whose doc comment promises no exception, threw `OutOfMemoryException` after
allocating up to that limit. Upstream sets no limit. The largest repeat in the vendored
`tests/data` is `%repeat[1053700 x x]%`, about 1 MiB.

## Decision

One `%repeat` may produce at most 16 MiB characters
(`UpstreamTestInstructions.MaximumRepeatLength`), checked from the decoded content's length
times the count before anything is allocated. A longer one is left as written and `%repeat` is
listed once in `UpstreamTestFileExpansion.UnsupportedInstructions`, so the screening skips the
case with a reason, as it does for `%days`, rather than running it with the wrong data.

## Why

- 16 times the largest vendored repeat leaves room for upstream to grow its tests, while a
  test run on a machine shared by nine lanes never allocates more than a few tens of MiB for
  one instruction.
- Listing it as unsupported keeps the expander's contract: what it cannot carry out is left as
  written and named, never thrown and never silently wrong.

## Consequences

No vendored case changes: `PassingUpstreamCases.txt` is unchanged. A future upstream case with
a repeat past 16 MiB is skipped with `%repeat` as its reason until the cap is raised here.
