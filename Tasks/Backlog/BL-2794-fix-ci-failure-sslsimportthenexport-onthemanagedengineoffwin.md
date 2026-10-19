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
completed:
---
# BL-2794 — Fix CI failure SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey on Linux and macOS

## Goal

`SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey` failed on Linux and macOS in CI run 39226925458 (https://github.com/StewartScottRogers/Curl/actions/runs/39226925458). First failing commit: de41660b.

    CollectionAssert.AreEqual failed. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 39226925458 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-19: Created.
