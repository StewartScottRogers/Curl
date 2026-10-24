---
id: BL-3407
title: Vendor upstream curl 8.22.0's configure.ac, lib and src C sources and the docs Markdown that test1165.pl and test1275.pl read
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3391, BL-3406]
touches: [Curl.Conformance.UnitTests/UpstreamTestData/configure.ac, Curl.Conformance.UnitTests/UpstreamTestData/lib, Curl.Conformance.UnitTests/UpstreamTestData/src, Curl.Conformance.UnitTests/UpstreamTestData/docs, Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1, Curl.Conformance.UnitTests/UpstreamTestData/.gitattributes, Curl.Conformance.UnitTests/UpstreamTestData/README.md, Curl.Conformance.UnitTests/UpstreamTestData/m4, Curl.Conformance.UnitTests/UpstreamTestData/CMakeLists.txt, Curl.Conformance.UnitTests/UpstreamTestData/include/README.md, Curl.Conformance.UnitTests/UpstreamTestData/projects/README.md, Curl.Conformance.UnitTests/UpstreamTestData/projects/Windows/README.md, Curl.Conformance.UnitTests/UpstreamTestData/tests, Curl.Conformance.UnitTests/UpstreamTestData/.github, Curl.Conformance.UnitTests/UpstreamTestData/CHANGES.md, Curl.Conformance.UnitTests/UpstreamTestData/GIT-INFO.md, Curl.Conformance.UnitTests/UpstreamTestData/SECURITY.md]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3407 — Vendor upstream curl 8.22.0's configure.ac, lib and src C sources and the docs Markdown that test1165.pl and test1275.pl read

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- The task file is an unfilled template; the spec is its title and ADR-0803. Vendored for test1165.pl (configure.ac, m4/*.m4, CMakeLists.txt, lib/curl_config-cmake.h.in, lib and src *.c/*.h, docs/CURL-DISABLE.md) and test1275.pl (every tracked *.md, which it reads through git ls-files). Update-UpstreamTestData.ps1 does it.
- Upstream root README.md is not vendored: it would overwrite this folder's README.md. The BL-3414 port of 1275 must treat the root README.md as absent.
- Second claim: the first run's integration failed only on the task file (its Doing -> Done rename); its two commits were cherry-picked unchanged. touches widened to the extra paths the work writes (.gitattributes, README.md, m4, CMakeLists.txt, tests, .github, root and include/projects Markdown); no other Doing task on origin/work/dark-factory names any UpstreamTestData path.
- No production code changes, so no Measure-CodeQuality run applies. Vendored files are byte-for-byte (-text), so output is platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Backlog. Lane 2 could not integrate: rebase failed. The work is on branch factory/BL-3407-lane-2-20261023-110315; start with git cherry-pick --no-commit factory/BL-3407-lane-2-20261023-110315 and fix it.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Done. configure.ac, m4, CMakeLists.txt, lib/src C sources and all Markdown vendored for cases 1165 and 1275
