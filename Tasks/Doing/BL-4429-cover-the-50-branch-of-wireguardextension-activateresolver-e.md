---
id: BL-4429
title: Cover the 50% branch of WireGuardExtension.ActivateResolver(ExtensionOptionValues)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.WireGuard.UnitLibrary/WireGuardExtension.cs, Curl.Extension.WireGuard.UnitTests/WireGuardExtensionTests.cs]
requirement: none
created: 2026-10-31
completed:
---
# BL-4429 — Cover the 50% branch of WireGuardExtension.ActivateResolver(ExtensionOptionValues)

## Goal

Cover both branches of the public ActivateResolver overload.

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] The public ActivateResolver is called twice so the compiler-cached File.ReadAllText delegate branch is taken both ways.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: fbecace0.
