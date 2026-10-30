---
id: BL-4105
title: Extension: Send GraphQL queries with --graphql
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.GraphQl.UnitLibrary, Curl.Extension.GraphQl.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4105 — Extension: Send GraphQL queries with --graphql

## Goal

Curl provides `--graphql <file>` with variables, introspection and readable error listing, as the extension `Curl.Extension.GraphQl.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 4 of 63. Specification: GraphQL over HTTP (graphql.github.io/graphql-over-http). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.GraphQl.UnitLibrary` and `Curl.Extension.GraphQl.UnitTests` exist in `/Extensions/GraphQl/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--graphql <file>` with variables, introspection and readable error listing.
- [x] Unit tests check its wire behaviour against named sections of GraphQL over HTTP (graphql.github.io/graphql-over-http), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 5): GraphQL needs no scheme, so it is a transfer hook (ADR-1007, ADR-1019), not a protocol handler: it registers by one line each in the option, description and transfer hook catalogues (shared list files beside ExtensionCatalogue.cs, added to `touches`), and adds no line to ExtensionCatalogue.cs, whose entries are scheme handlers. The first criterion is read that way.
- Decisions (ADR-1026; 1021 was taken on the shared branch): options `--graphql <file>`, `--graphql-variables <json|@file>`, `--graphql-operation <name>`, `--graphql-introspect`; always POST `application/json` with `Accept: application/graphql-response+json, application/json;q=0.9` unless `-H` gave an Accept; body passes to output unchanged; errors listed one line each on stderr (`Console.Error`); unreadable file or non-object variables fail with exit 26. GET, stdin and batching left out (ADR-1026).
- Checked: `curl --extensions` and `--ai-help extensions` list it; `CURL_EXTENSIONS=0 curl --graphql x URL` prints "option --graphql: is unknown" and exits 2; without the options no hook is activated (ActivatedBy gives null), so transfers are unchanged. A loopback end-to-end probe could not listen in this sandbox; the wire bytes are pinned by GraphQlTransferHookTests through a fake inner transfer.
- Measure-CodeQuality (GraphQl + Composition): both 100% line and branch, 0 failing members. Fast tests: 55 projects, 46515 passed, 0 failed. Platform-neutral: temp files via Path.GetTempFileName, no OS text pinned; CI on Linux and macOS is left to the shift watch.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d492b5c0 27221cd7 9183630f e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. curl --graphql <file> sends a GraphQL-over-HTTP POST with variables, operation name or introspection, and lists response errors on stderr
