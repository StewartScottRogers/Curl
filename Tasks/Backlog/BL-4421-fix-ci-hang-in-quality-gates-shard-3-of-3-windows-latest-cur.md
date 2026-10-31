---
id: BL-4421
title: Fix CI hang in Quality gates shard 3 of 3 (windows-latest) (Curl.Console.UnitTests)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-31
completed:
---
# BL-4421 — Fix CI hang in Quality gates shard 3 of 3 (windows-latest) (Curl.Console.UnitTests)

## Goal

`Quality gates shard 3 of 3 (windows-latest)` finishes well inside its `timeout-minutes` on every `CI` run; in CI run 41228308093 it hung while `Curl.Console.UnitTests` was running.

## Context

Filed by the dark factory's CI watch (BL-4420). In CI run 41228308093 (https://github.com/StewartScottRogers/Curl/actions/runs/41228308093) on commit 23bacbc6, job `Quality gates shard 3 of 3 (windows-latest)` ran into its `timeout-minutes` and GitHub cancelled it, so the run reads as cancelled, not failed. The last test assembly it started was `Curl.Console.UnitTests`.

    Last log line at 2026-10-31 18:33:26 UTC: Skipped RemoveFile_DevNullOffWindows_IsNotRegularFile - then nothing for 39 min until ##[error]The operation was canceled.

A hang can be intermittent: the next run may pass. Reproduce with `dotnet test` on that project and `--blame-hang-timeout 3m`, and read the job's whole log with `gh run view 41228308093 --log`.

## Acceptance criteria

- [ ] What made `Curl.Console.UnitTests` hang in `Quality gates shard 3 of 3 (windows-latest)` is named under Notes and removed.
- [ ] `Quality gates shard 3 of 3 (windows-latest)` finishes inside its `timeout-minutes` in the `CI` workflow for the commit that lands the fix.

## Notes

## Log

- 2026-10-31: Created.
