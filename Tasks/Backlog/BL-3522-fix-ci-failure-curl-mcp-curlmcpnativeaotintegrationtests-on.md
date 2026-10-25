---
id: BL-3522
title: Fix CI failure [Curl.Mcp.CurlMcpNativeAotIntegrationTests on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.IntegrationTests, Curl.Mcp.UnitLibrary, Curl.Mcp]
requirement: none
created: 2026-10-25
completed:
---
# BL-3522 — Fix CI failure [Curl.Mcp.CurlMcpNativeAotIntegrationTests on Windows

## Goal

`[Curl.Mcp.CurlMcpNativeAotIntegrationTests` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `[Curl.Mcp.CurlMcpNativeAotIntegrationTests` failed on Windows in CI run 40228351849 (https://github.com/StewartScottRogers/Curl/actions/runs/40228351849). First failing commit: 740d949e.

    Class Cleanup method CurlMcpNativeAotIntegrationTests.DeletePublishFolder failed. Error Message: System.IO.IOException: The process cannot access the file 'Curl.Mcp.exe' because it is being used by another process.. Stack Trace:    at System.IO.FileSystem.RemoveDirectoryRecursive(String fullPath, WIN32_FIND_DATA& findData, Boolean topLevel)

Lanes test only on Windows, so reproduce with `gh run view 40228351849 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `[Curl.Mcp.CurlMcpNativeAotIntegrationTests` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-25: Created.
