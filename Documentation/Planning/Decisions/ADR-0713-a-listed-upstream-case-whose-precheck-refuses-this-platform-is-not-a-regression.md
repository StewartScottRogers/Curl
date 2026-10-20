# ADR-0713 — A listed upstream case whose precheck refuses this platform is not a regression

- **Status:** Accepted
- **Date:** 2026-10-19

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2828.

## Context

ADR-0488 lets a listed case be `Inconclusive`, not failed, where it is skipped only because another
platform's Curl has the features it needs. test2072 ("file:// with Unix path for the case of extra
slashes") needs no feature Windows lacks: its `<client><precheck>` is a `%PERL -e` one-liner that
prints `Test requires a Unix system` when Perl's `$^O` is `MSWin32`, `cygwin`, `dos` or `msys`, and
`runtests.pl` skips the case with that line. So on Windows a listed 2072 would fail the ratchet as
"now skipped", and unlisted it can regress on Linux and macOS unnoticed.

The runner (`UpstreamCaseRunner`) could instead rerun the precheck with every other platform's `$^O`
and mark the outcome `SkippedOnThisPlatform`, but that file belonged to another live task (BL-2863)
when this was decided.

## Decision

`UpstreamCaseRatchet.Judge` treats a skip whose detail is a line an upstream precheck prints to
refuse the platform `$^O` names - today only `Test requires a Unix system`, the one such line in
the vendored cases at `curl-8_22_0` - as a case that runs only on another platform: `Inconclusive`
with "runs only on another platform" when listed, as ADR-0488's feature skips are. On Linux and
macOS the precheck prints nothing, so the case runs and is judged by its run.

## Consequences

- 2072 is on `PassingUpstreamCases.txt` and held to it on Linux and macOS.
- A new upstream precheck refusing a platform with different text needs its line added to the
  ratchet's list; until then a listed case with it fails on the refused platform, which shows it.
