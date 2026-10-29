---
id: BL-4107
title: Extension: Build requests from an OpenAPI description with --openapi
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4205, BL-4209]
touches: [Curl.Extension.OpenApi.UnitLibrary, Curl.Extension.OpenApi.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionArgumentExpanderCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4107 — Extension: Build requests from an OpenAPI description with --openapi

## Goal

Curl provides `--openapi <spec> --operation <id> name=value...`, as the extension `Curl.Extension.OpenApi.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 6 of 63. Specification: OpenAPI Specification 3.1. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.OpenApi.UnitLibrary` and `Curl.Extension.OpenApi.UnitTests` exist in `/Extensions/OpenApi/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `--openapi <spec> --operation <id> name=value...`.
- [ ] Unit tests check its wire behaviour against named sections of OpenAPI Specification 3.1, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 prerequisite pass (lane 6, no code edited): `--openapi <spec> --operation <id> name=value...` must supply a whole request (the server URL from the description, the method, path, query and header parameters, a request body), but no extension hook can supply a transfer's URL, method or body (`ITransferHook`, ADR-1007, only adds headers and wraps streams), and a command line without a URL stops at "no URL specified" (exit 2). BL-4205 (Backlog) adds argument expanders that turn an option's value into curl arguments; its contract hands an expander only its own value, so `--operation <id>` and every `name=value` would still be parsed as an option and URLs (`http://petId=5`). Filed BL-4209 (depends on BL-4205) to let an expander take companion options and the matching positional arguments after its option. Planned shape once both land: `OpenApiArgumentExpander` reads the JSON description with `System.Text.Json` (BCL), resolves local `$ref`s, picks `servers[0]` with variable defaults (OAS 3.1 §4.8.5), serializes parameters by `style`/`explode` (§4.8.12.4), sends a `name=value` that is no parameter as a JSON body property (§4.8.13), and expands into `-X`, `-H`, `--data-binary` and the URL. Added `ExtensionOptionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs` and `ExtensionArgumentExpanderCatalogue.cs` to `touches`: the extension registers its options, description and expander one line each (shared list files, BL-4205 creates the last). YAML descriptions are left for that run to decide (a hand-written YAML reader, its own task if too large).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 2741ec08 0571f249 76c078af e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4205 (argument expanders supplying URL, method and body) and BL-4209 (expander taking --operation and name=value arguments)
