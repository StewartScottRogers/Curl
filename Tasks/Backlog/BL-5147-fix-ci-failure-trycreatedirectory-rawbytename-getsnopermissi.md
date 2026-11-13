---
id: BL-5147
title: Fix CI failure TryCreateDirectory_RawByteName_GetsNoPermissionBeyondMode0750 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-13
completed:
---
# BL-5147 — Fix CI failure TryCreateDirectory_RawByteName_GetsNoPermissionBeyondMode0750 on Linux and macOS

## Goal

`TryCreateDirectory_RawByteName_GetsNoPermissionBeyondMode0750` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `TryCreateDirectory_RawByteName_GetsNoPermissionBeyondMode0750` failed on Linux and macOS in CI run 43504931912 (https://github.com/StewartScottRogers/Curl/actions/runs/43504931912). First failing commit: 3296a664.

    TestCleanup method Curl.Console.PhysicalOutputPathsDirectoryModeTests.DeleteRoot threw exception. System.IO.DirectoryNotFoundException: Could not find a part of the path '/tmp/curl-bl5097-c21e97036eb542869faefd5e363ee630/r�x'..

Lanes test only on Windows, so reproduce with `gh run view 43504931912 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `TryCreateDirectory_RawByteName_GetsNoPermissionBeyondMode0750` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-13: Created.
