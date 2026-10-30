---
id: BL-4322
title: Fix CI failure EndPoint_Serialize_CarriesTheStructureAfterItsFamily on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.VirtualMachineSocket.UnitTests, Curl.Extension.VirtualMachineSocket.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4322 — Fix CI failure EndPoint_Serialize_CarriesTheStructureAfterItsFamily on Linux and macOS

## Goal

`EndPoint_Serialize_CarriesTheStructureAfterItsFamily` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `EndPoint_Serialize_CarriesTheStructureAfterItsFamily` failed on Linux and macOS in CI run 41002071588 (https://github.com/StewartScottRogers/Curl/actions/runs/41002071588). First failing commit: 41bb1879.

    Test method Curl.Extension.VirtualMachineSocket.VirtualMachineSocketAddressTests.EndPoint_Serialize_CarriesTheStructureAfterItsFamily threw exception:

Failing data rows: `Hyper-V`, `vsock`.

Lanes test only on Windows, so reproduce with `gh run view 41002071588 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `EndPoint_Serialize_CarriesTheStructureAfterItsFamily` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d54d78cb.
