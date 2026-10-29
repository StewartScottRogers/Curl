---
id: BL-4154
title: Extension: Run .http request files with --http-file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4205]
touches: [Curl.Extension.HttpFile.UnitLibrary, Curl.Extension.HttpFile.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4154 — Extension: Run .http request files with --http-file

## Goal

Curl provides `--http-file <file>` with variables and named environments, as the extension `Curl.Extension.HttpFile.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 54 of 63. Specification: the Visual Studio / VS Code REST Client .http file format. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.HttpFile.UnitLibrary` and `Curl.Extension.HttpFile.UnitTests` exist in `/Extensions/HttpFile/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `--http-file <file>` with variables and named environments.
- [ ] Unit tests check its wire behaviour against named sections of the Visual Studio / VS Code REST Client .http file format, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 prerequisite pass (lane 7, no code edited): a .http file supplies whole requests (method, URL, headers, body, several separated by `###`), but no extension hook can supply a transfer's URL, method or body. `ITransferHook` (ADR-1007) only adds headers, wraps the body stream, reads the result and repeats; BL-4204 (Doing) lets a hook answer or replace a response, not define the request. A command line holding only `--http-file f` also stops at "no URL specified" (exit 2) before any transfer exists. Filed BL-4205 (an argument expander: an extension option expands into curl arguments, `-X`/`-H`/`--data-binary`/URL/`--next`, before Curl.Console parses) and added it to `depends-on`. Once it is Done, this task is one run: a .http parser (requests split on `###`, `@name = value` file variables, `{{name}}` substitution, `{{$processEnv NAME}}`, named environments from `http-client.env.json` / `rest-client.environmentVariables` read with System.Text.Json, BCL) that produces those arguments, the expander and option registered one line each, tests on recorded text, the ADR. The environment is chosen how is for the next run's ADR (for example `--http-file f` plus a `--http-env <name>` option).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ff504e0d c18cbc4b 6af91697 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits for BL-4205: no extension hook can supply a transfer's URL, method or body, which a .http file needs
