---
id: BL-5747
title: Remove a Curl.Api request header whose value is empty, as curl -H "Name:" does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlRequestOptions.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5747 — Remove a Curl.Api request header whose value is empty, as curl -H "Name:" does

## Goal

A `Curl.Api` header given with an empty value (`new("grpc-timeout", "")`) removes that header from the request, as `curl -H "grpc-timeout:"` does, instead of sending it empty.

## Context

From Surly cookbook bug report `2026-11-22T0726-Curl-bug-report.md` (batch 10 of the 1,000-recipe plan), finding 3. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`Headers = { new("grpc-timeout", "") }` sends the header with an empty value, and the server answers INTERNAL `malformed grpc-timeout`; `curl -H "grpc-timeout:"` removes it. `RemovedHeaders` already removes headers. BL-5598 (Done) made the same rule hold for the `curl-mcp` `transfer` tool. curl sends an empty header only for `-H "Name;"`: decide (ADR if not obvious) whether an empty value means "remove", matching `-H`, and how a `Curl.Api` user sends a header empty on purpose. Cookbook recipe: `grpc-time-out-a-slow-call-in-csharp`.

## Acceptance criteria

- [ ] A `Curl.Api` request with a header whose value is empty sends no such header, pinned by a test of the request bytes; `RemovedHeaders` still works.
- [ ] The way to send an empty header on purpose is documented on the property and in the README.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0726, finding 3.
- 2026-11-22: Backlog -> Doing. Criteria at claim: eecfb519 b6e24e06 3d836d5e.
