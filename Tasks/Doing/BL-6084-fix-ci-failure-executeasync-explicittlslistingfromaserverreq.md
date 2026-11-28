---
id: BL-6084
title: Fix CI failure ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitTests, Curl.Original.Protocol.Ftp.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-6084 — Fix CI failure ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing on Linux

## Goal

`ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing` failed on Linux in CI run 46102818546 (https://github.com/StewartScottRogers/Curl/actions/runs/46102818546). First failing commit: 4e954e9f.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 46102818546 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExecuteAsync_ExplicitTlsListingFromAServerRequiringSessionReuse_WritesTheListing` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 22f846bf.
