---
id: BL-3993
title: Fix CI failure ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-3993 — Fix CI failure ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy on Linux and macOS

## Goal

`ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy` failed on Linux and macOS in CI run 40774593052 (https://github.com/StewartScottRogers/Curl/actions/runs/40774593052). First failing commit: 81764fdf.

    CollectionAssert.AreEqual failed. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 40774593052 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
