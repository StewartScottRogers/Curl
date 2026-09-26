---
id: BL-145
title: Substitute test-case variables and evaluate %if blocks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-144]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-145 — Substitute test-case variables and evaluate %if blocks

## Goal

A parsed upstream test case can be expanded for one run: its `%VARIABLES` replaced with the
run's values and its `%if` / `%else` / `%endif` blocks resolved against the features Curl
reports.

## Context

- ADR-0013, decision 4: `%LOGDIR` is an absolute, per-case temporary directory so cases run in
  parallel; host and port variables name the in-memory servers.
- Variables and conditionals are described in `docs/tests/FILEFORMAT.md` at `curl-8_21_0`
  (https://github.com/curl/curl/blob/curl-8_21_0/docs/tests/FILEFORMAT.md): at least
  `%TESTNUMBER`, `%LOGDIR`, `%HOSTIP`, `%HTTPPORT`, `%FTPPORT`, `%SRVDIR`, `%PWD`, `%CLIENTIP`,
  `%repeat[…]%` and `%if <feature>` / `%if !<feature>` / `%else` / `%endif`.
- The feature set is an input (a set of names), not read from `curl --version` here; BL-147
  supplies it.
- Upstream preprocesses the whole file before `getpart.pm` reads it, and `%if` blocks can wrap
  whole parts. BL-144's `UpstreamTestCaseParser` ignores `%if` lines outside a part, so a case
  with such a block parses with the parts of both branches: expand the file's bytes first and
  parse the result, rather than expanding an already-parsed `UpstreamTestCase` (BL-144 Notes).
- An unknown variable is left as is and reported, so a case using one can be skipped with a
  reason.

## Acceptance criteria

- [ ] `UpstreamTestCaseExpander` (or a name that says what it does) takes an `UpstreamTestCase`,
      the variable values and the feature set, and returns the expanded case; tests in
      `Curl.Conformance.UnitTests` cover every variable and form listed in Context, nested `%if`,
      and `%if !feature`.
- [ ] Unknown variables are listed on the result (tested).
- [ ] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10 per
      method, per `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
