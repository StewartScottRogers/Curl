---
id: BL-4205
title: Let an extension option expand into curl command-line arguments
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Arguments, Curl.Extension.Abstractions.UnitTests/Arguments, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md, Curl.Extension.Composition.UnitLibrary/ExtensionArgumentExpanderCatalogue.cs, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, Curl.Extension.Composition.UnitTests/ExtensionArgumentExpanderCatalogueTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerArgumentExpansionTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4205 — Let an extension option expand into curl command-line arguments

## Goal

An extension can register an argument expander that turns its option's value (such as `--http-file requests.http`) into curl command-line arguments (URLs, `-X`, `-H`, `--data-binary`, `--next`), which Curl.Console parses in place of that option, so an extension can supply whole requests; with no expander's option given, every command line parses exactly as before.

## Context

Found by BL-4154 (`--http-file`, the VS Code / Visual Studio REST Client .http format) in its prerequisite pass. The extension hooks (ADR-1006 options, ADR-1007 transfer hooks, ADR-1010 connection hooks, ADR-1012 resolver hooks, ADR-1016 certificate hooks) can add request headers and see the body, but none can supply a transfer's URL, method or request body, and a command line holding only `--http-file f` stops at curl's "no URL specified" (exit 2) before any transfer exists. Suggested shape (decide in an ADR): in `Curl.Extension.Abstractions.UnitLibrary/Arguments/`, `IArgumentExpander` with the `ExtensionOption` it expands and `IReadOnlyList<string> ArgumentsFor(string value, ArgumentExpansionContext context)` (the context reads files and environment variables through injected delegates, so tests need no disk), an exception type whose message the host prints as `curl: <message>` with exit 2 (or 26 when the file cannot be read, as `-K` does); `Curl.Extension.Composition.UnitLibrary/ExtensionArgumentExpanderCatalogue.cs`, a one-entry-per-line list (add it to `Tasks/SharedListFiles.txt` and `.gitattributes` `merge=union` as `ExtensionCatalogue.cs` is, or leave that to the interactive session if those files are guarded); and in `Curl.Console/CurlCommandRunner.cs` near `CommandLineParser.Parse` (line ~1516), replace each expander option and its value with its expansion before parsing, read none under `CURL_EXTENSIONS=0` (the option is then refused as unknown, exit 2). The option itself stays registered in `ExtensionOptionCatalogue` so `--extensions` and `--ai-help extensions` list it.

## Acceptance criteria

- [ ] `Curl.Extension.Abstractions.UnitLibrary/Arguments` holds the expander contract, and `Curl.Extension.Composition.UnitLibrary/ExtensionArgumentExpanderCatalogue.cs` lists expanders one per line.
- [ ] Curl.Console replaces an expander's option and value with the expander's arguments before parsing; unit test with a test expander producing `-X POST -H "A: b" --data-binary x http://h/ --next http://h/2` checks the two transfers the parser builds.
- [ ] An expander's failure ends the run with `curl: <message>` and the expander's exit code before any transfer; unit test.
- [ ] With `CURL_EXTENSIONS=0` an expander's option is refused exactly as an unknown option (exit 2), and a command line without any expander's option parses unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the contract's shape and where expansion happens, and both libraries' `CLAUDE.md` describe it.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
