---
id: BL-4154
title: Extension: Run .http request files with --http-file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4205]
touches: [Curl.Extension.HttpFile.UnitLibrary, Curl.Extension.HttpFile.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionArgumentExpanderCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4154 — Extension: Run .http request files with --http-file

## Goal

Curl provides `--http-file <file>` with variables and named environments, as the extension `Curl.Extension.HttpFile.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 54 of 63. Specification: the Visual Studio / VS Code REST Client .http file format. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.HttpFile.UnitLibrary` and `Curl.Extension.HttpFile.UnitTests` exist in `/Extensions/HttpFile/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--http-file <file>` with variables and named environments.
- [x] Unit tests check its wire behaviour against named sections of the Visual Studio / VS Code REST Client .http file format, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 prerequisite pass (lane 7, no code edited): a .http file supplies whole requests (method, URL, headers, body, several separated by `###`), but no extension hook can supply a transfer's URL, method or body. `ITransferHook` (ADR-1007) only adds headers, wraps the body stream, reads the result and repeats; BL-4204 (Doing) lets a hook answer or replace a response, not define the request. A command line holding only `--http-file f` also stops at "no URL specified" (exit 2) before any transfer exists. Filed BL-4205 (an argument expander: an extension option expands into curl arguments, `-X`/`-H`/`--data-binary`/URL/`--next`, before Curl.Console parses) and added it to `depends-on`. Once it is Done, this task is one run: a .http parser (requests split on `###`, `@name = value` file variables, `{{name}}` substitution, `{{$processEnv NAME}}`, named environments from `http-client.env.json` / `rest-client.environmentVariables` read with System.Text.Json, BCL) that produces those arguments, the expander and option registered one line each, tests on recorded text, the ADR. The environment is chosen how is for the next run's ADR (for example `--http-file f` plus a `--http-env <name>` option).
- 2026-10-29 (lane 13): Done as an argument expander (ADR-1040, ADR-1051), recorded in ADR-1073. `HttpFileArgumentExpander` (an `ITrailingArgumentExpander`) replaces `--http-file <file>` and a companion `--http-env <name>` with each request's curl arguments, joined by `--next`; `--http-env` reads `http-client.env.json` beside the file (`$shared` first). Like GraphQL (BL-4105), it needs no scheme, so it adds no line to ExtensionCatalogue.cs, whose entries are scheme handlers; it registers one line each in the option (two options), description and argument expander catalogues, shared list files added to `touches`. The first criterion is read that way. Defaults taken: file variables override environment ones; an unknown `{{...}}` (dynamic system variables, request variables) fails with exit 2 rather than send a literal; inline bodies go as `--data-raw`, `< path` bodies as `--data-binary @path`.
- Checks: 48 tests in Curl.Extension.HttpFile.UnitTests (text normalised to LF in tests, path splitting keeps the separator written, so platform-neutral); Measure-CodeQuality: HttpFile 100/100, 0 failing (after splitting two methods past complexity 10), Composition 100/100, 0 failing. Real binary: `--extensions` and `--ai-help extensions` list it, `CURL_EXTENSIONS=0 curl --http-file x` gives "option --http-file: is unknown", exit 2, as `--bogus-opt` does. Build clean. The full fast run was green except 10 timing failures in Curl.Quic.UnitTests (untouched), which passed 506/506 alone; filed BL-4273.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ff504e0d c18cbc4b 6af91697 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits for BL-4205: no extension hook can supply a transfer's URL, method or body, which a .http file needs
- 2026-10-29: Backlog -> Doing. Criteria at claim: ff504e0d c18cbc4b 6af91697 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. curl --http-file runs a .http file's requests with file variables, processEnv and http-client.env.json environments chosen by --http-env
