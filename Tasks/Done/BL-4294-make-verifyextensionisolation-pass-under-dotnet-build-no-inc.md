---
id: BL-4294
title: Make VerifyExtensionIsolation pass under dotnet build --no-incremental
priority: High
assignee: Claude
pipeline: bugfix
depends-on: []
touches: [Directory.Build.props]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4294 — Make VerifyExtensionIsolation pass under dotnet build --no-incremental

## Goal

`dotnet build --no-incremental` of the whole solution succeeds, as a plain `dotnet build` already does, while `VerifyExtensionIsolation` still refuses a project's own direct reference to an extension.

## Context

`VerifyExtensionIsolation` in `Directory.Build.props` (ADR-0999, added by BL-4079 in f7b52c9e0) runs `BeforeTargets="BeforeBuild"`, on the assumption that the SDK has not yet added transitive project references to `@(ProjectReference)` there. That holds for a plain build, but not under `--no-incremental` (the Rebuild target): measured 2026-10-29 on work/dark-factory at f727694f4, `dotnet build Curl.Conformance.UnitTests --no-incremental` fails with "Curl.Conformance.UnitTests references Curl.Extension.Abstractions.UnitLibrary;Curl.Extension.Amqp0.UnitLibrary;... but only the hosts (Curl.Console, Curl.Api, Curl.Mcp) and the extensions reference an extension (ADR-0999)", while `dotnet build Curl.Conformance.UnitTests` passes. Whole-solution `--no-incremental` fails the same way for Curl.Conformance.UnitTests, Curl.Conformance.Differential.IntegrationTests and Curl.Mcp.IntegrationTests, which reach the extensions only transitively through Curl.Console or Curl.Mcp.

This breaks `RunDarkFactory.cmd -ProbeMachine`, which builds with `--no-incremental`: on 2026-10-29 it measured one lane as failing and wrote a cap of 1 to `<repo>.lanes\machine-lanes.json` (the 2026-09-29 measurement, cap 13, was restored by hand). The machine probe cannot be rerun until this is fixed.

Likely fix: check only the project's own references, not transitive ones, e.g. by skipping items the SDK marks as transitive, or by capturing `@(ProjectReference)` at evaluation in a `Directory.Build.targets` before any target runs. Measure which works under both Build and Rebuild.

## Acceptance criteria

- [x] `dotnet build --no-incremental` of the solution exits 0 on Windows.
- [x] A scratch project outside the hosts and extensions that references an extension directly still fails the build with the ADR-0999 message, under both `dotnet build` and `dotnet build --no-incremental` (checked by hand and recorded in the Log; the scratch project is not committed).
- [x] An extension library referencing another extension, or a `Curl.Protocol.*` library, still fails the build under both.
- [x] CI is green on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 49975296 2e3569be 9239b4d0 fd900c2c.
- 2026-10-29: Fix b0e3fdc4d: VerifyExtensionIsolation now runs BeforeTargets="IncludeTransitiveProjectReferences;BeforeBuild" (Rebuild's Clean ran that SDK target before BeforeBuild). Whole-solution `dotnet build --no-incremental`: 0 warnings, 0 errors (was 3 errors). Fast tests: 74 assemblies passed, exit 0. Scratch projects (deleted, not committed), each under `dotnet build` and `--no-incremental`: Curl.ScratchProbe.UnitLibrary -> Curl.Extension.Coap gives "only the hosts ... reference an extension (ADR-0999)"; Curl.Extension.ScratchProbe.UnitLibrary -> Curl.Protocol.Http gives "an extension references no curl protocol library"; -> Curl.Extension.Mqtt5 alone gives "an extension references no other extension". All four runs per rule failed as required.
- 2026-10-29: CI run 40980505280 on work/dark-factory at 0af3afe15 (contains b0e3fdc4d) passed on Windows, Linux and macOS.
- 2026-10-29: Doing -> Done. Rebuild passes; isolation rules still enforced; CI green on all three
