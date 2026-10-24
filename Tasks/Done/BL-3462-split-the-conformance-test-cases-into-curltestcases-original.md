---
id: BL-3462
title: Split the conformance test cases into CurlTestCases/Original (upstream tests/data) and CurlTestCases/Extended (our own)
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamTestData, Curl.Conformance.UnitTests/CurlTestCases, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj, Curl.Conformance.UnitTests/Upstream*.cs, Curl.Conformance.UnitTests/CLAUDE.md, Curl.Conformance.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-24
completed: 2026-10-24
---
# BL-3462 — Split the conformance test cases into CurlTestCases/Original (upstream tests/data) and CurlTestCases/Extended (our own)

## Goal

The `.curltest` cases copied from upstream curl live in `Curl.Conformance.UnitTests/CurlTestCases/Original/`
and the cases we write ourselves live in its sibling `CurlTestCases/Extended/`, so the upstream copy can be
refreshed from a curl release tag without touching our own cases.

## Context

Stewart, 2026-10-24 (approved): "move these files into a subdirectory so that all of these tracking files
can be managed separately from our extended testing using our own derived and created files ... two
different sub directories need to be siblings and named for their purpose [original, extended]."

Today all 2072 `test*.curltest` files sit in `Curl.Conformance.UnitTests/UpstreamTestData/`, byte for byte
from curl's `tests/data` at `curl-8_22_0` (ADR-0013 decision 3, BL-2157), beside the upstream support
folders (`certs/`, `data/`, `docs/`, `include/`, `lib/`, `m4/`, `projects/`, `src/`, `tests/`, `.github/`),
`COPYING`, `README.md`, `.gitattributes` and `Update-UpstreamTestData.ps1`. None of our own cases exist yet,
so `Extended/` starts with a README only. About 42 live files name `UpstreamTestData` (csproj copy items,
the case runner and its tests, the update script, CLAUDE.md files, ADRs); archived tasks and audit
findings are history and keep the old path.

Decisions made with Stewart's approval: PascalCase `Original` and `Extended`; parent `CurlTestCases/`
(the old name `UpstreamTestData` would be untrue once our own cases sit inside it); our own cases are named
`ext<number>.curltest` so a number never collides with an upstream case.

## Acceptance criteria

- [x] `git mv` moves everything in `UpstreamTestData/` to `CurlTestCases/Original/` with no content change (`git diff -M --stat` shows only renames for those files), and `UpstreamTestData/` no longer exists.
- [x] `CurlTestCases/Extended/README.md` says what goes there, the `ext<number>.curltest` naming, that cases use the `tests/data` format, and that they run with `Original/`'s `certs/` and `data/` as `%CERTDIR` and `%SRCDIR`.
- [x] `Update-UpstreamTestData.ps1` (now in `Original/`) writes only into `Original/` and never touches `Extended/`; its help says so.
- [x] The case runner loads `ext*.curltest` from `Extended/` as well as `test*.curltest` from `Original/`; a focused unit test with one throwaway `ext` case in a temporary folder proves an Extended case is found and run.
- [x] `UpstreamTestDataTests` still asserts exactly 2072 upstream cases, counted in `Original/` only; `PassingUpstreamCases.txt` and the upstream pass ratchet count only `Original/` cases, so Extended cases never change the upstream pass count or the gap office's behaviour score.
- [x] Every live reference to `UpstreamTestData` (csproj, `.cs`, CLAUDE.md, READMEs, ADRs still in force) names the new path; `git grep UpstreamTestData` hits only `Tasks/Done`, `Audit/Findings`, `Audit/Scorecards` and ADRs that are history. If a live Audit or Gap path needs the change, stop and leave that part to an interactive session.
- [x] An ADR records the split and the `ext` naming, "Decided by Claude under Stewart's delegation", approved by Stewart 2026-10-24.
- [x] `dotnet build` is clean and the fast tests pass with the same counts of passed and skipped upstream cases as before the move.

## Notes

- Also touched, one line each: Documentation/Product/Product-Overview.md (path), Tasks/Deferred/BL-3412 (path in its text), Curl.Conformance.UnitLibrary/CLAUDE.md.
- Ext cases are not a DynamicData row set (an empty set fails MSTest); one method runs them all. Output copies of docs/include/lib/src/m4 now sit in CurlTestCases/ (the %SRCDIR parent), checked by the beside-SrcDir test.
- Fast tests: 4360 passed, 0 failed, 87 skipped (same skips as the failing-free run). ADR-0810. CI platforms: change is path-neutral.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: f2d4fc49 e3e7bd70 641ada7b 6ad4c3b6 26214cae 91ff8df9 338e5e58 1b8306bd.
- 2026-10-24: Doing -> Done. Conformance cases split into CurlTestCases/Original and Extended; ext cases run, upstream count unchanged
