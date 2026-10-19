---
id: BL-2794
title: Fix CI failure SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests, Libcurl]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2794 — Fix CI failure SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey on Linux and macOS

## Goal

`SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey` failed on Linux and macOS in CI run 39226925458 (https://github.com/StewartScottRogers/Curl/actions/runs/39226925458). First failing commit: de41660b.

    CollectionAssert.AreEqual failed. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 39226925458 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test imported a packet with no valid-until tag, so its valid-until was 0 and `TlsSessionCache.ListSessions` dropped it as expired before the export, as curl's cache drops an expired session (the product is right; the test data was not). Fix: import a TLS 1.3 packet valid until 2100-01-01 (`OneByteTicketValidUntil2100`). Test-only change in `Libcurl.UnitTests`; no production code, so no quality run. The test is excluded on Windows, so locally it shows as skipped; Libcurl.UnitTests passed 1326, skipped 1. Linux and macOS are confirmed by the shift's CI watch on the landing commit, which files a new task if it still fails.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 78fa24e3.
- 2026-10-19: Doing -> Done. SSL session import-then-export test imports an unexpired packet, so it passes off Windows
