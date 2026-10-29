---
id: BL-4287
title: Extension: read YAML OpenAPI descriptions with --openapi
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4107]
touches: [Curl.Extension.OpenApi.UnitLibrary/OpenApiYaml.cs, Curl.Extension.OpenApi.UnitLibrary/OpenApiDocument.cs, Curl.Extension.OpenApi.UnitTests/OpenApiYamlTests.cs, Curl.Extension.OpenApi.UnitTests/OpenApiDocumentTests.cs, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4287 — Extension: read YAML OpenAPI descriptions with --openapi

## Goal

`curl --openapi spec.yaml --operation <id> name=value...` reads a YAML OpenAPI description exactly as it reads the same description in JSON.

## Context

BL-4107 added `Curl.Extension.OpenApi.UnitLibrary` (ADR-1083), which reads JSON descriptions only with `System.Text.Json`; a YAML one fails with "openapi: <file> is not a JSON OpenAPI description" (exit 2). OpenAPI 3.1 §4.3 allows either, and YAML is the more common. Base class library only, so write a YAML reader by hand in `OpenApiYaml.cs` (YAML 1.2 JSON schema subset OpenAPI §4.3.2 requires: block and flow mappings and sequences, plain, single- and double-quoted and block scalars, comments; anchors and tags may be refused with exit 2), converting to JSON bytes that `OpenApiDocument.Parse` already takes, chosen when the first non-blank character is not `{`.

## Acceptance criteria

- [ ] `OpenApiYamlTests` checks each YAML construct above against YAML 1.2.2's named sections, and a YAML description gives the same curl arguments as its JSON twin in `OpenApiDocumentTests`.
- [ ] ADR-1083's "Left out" no longer lists YAML; the README row is unchanged or updated to match.
- [ ] `Curl.Extension.OpenApi.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f2752b60 6fb3b176 6543a87b 3d836d5e.
