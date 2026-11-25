---
id: BL-5923
title: Make dotnet format --verify-no-changes pass across the solution
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: []
requirement: none
created: 2026-11-24
completed:
---
# BL-5923 — Make dotnet format --verify-no-changes pass across the solution

## Goal

`dotnet format --verify-no-changes` exits 0 at the repository root.

## Context

Found by the `verify` run of BL-5919 on 2026-11-24: `dotnet format --verify-no-changes` exited 2 with 1496 error lines (mostly `WHITESPACE`) across many projects, for example `Curl.Cli.UnitLibrary/ConfigFileSyntax.cs`, `Curl.Api/Client/CurlClient.cs` and `Curl.Conformance.Differential.UnitLibrary`. `touches` is empty because the reformat spans the solution, so the task runs alone.

## Acceptance criteria

- [ ] `dotnet format --verify-no-changes` exits 0 at the repository root.
- [ ] `dotnet build` is clean and the fast tests pass after the reformat.

## Notes

## Log

- 2026-11-24: Created.
