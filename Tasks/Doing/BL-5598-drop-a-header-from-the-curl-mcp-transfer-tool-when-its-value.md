---
id: BL-5598
title: Drop a header from the curl-mcp transfer tool when its value is empty, as curl -H "Name:" does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.UnitLibrary/McpTransferHeader.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5598 — Drop a header from the curl-mcp transfer tool when its value is empty, as curl -H "Name:" does

## Goal

A `transfer` header with an empty value removes that header from the request, as `curl -H "Accept:"` does, instead of sending it empty.

## Context

From Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #7. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

An empty `Accept` value in the `transfer` tool sends `Accept:` with an empty value, where `curl -H "Accept:"` drops the header. curl sends an empty header only for `-H "Name;"`. Decide how the tool expresses "send it empty" (for example a separate flag), and document both in the tool description.

## Acceptance criteria

- [ ] A transfer with header `Accept` set to an empty value sends no `Accept` header, pinned by a test of the request bytes.
- [ ] The tool description says how to remove a default header and how (if at all) to send an empty one.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug reports (2203 #7).
- 2026-11-20: Backlog -> Doing. Criteria at claim: b0a4577d 9756089e 3d836d5e.
