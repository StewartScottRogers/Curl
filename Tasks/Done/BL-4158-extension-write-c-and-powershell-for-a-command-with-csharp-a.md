---
id: BL-4158
title: Extension: Write C# and PowerShell for a command with --csharp and --powershell
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.CodeGeneration.UnitLibrary, Curl.Extension.CodeGeneration.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4158 — Extension: Write C# and PowerShell for a command with --csharp and --powershell

## Goal

Curl provides `--csharp <file>` and `--powershell <file>`, the way `--libcurl` writes C, as the extension `Curl.Extension.CodeGeneration.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 58 of 63. Specification: HttpClient and Invoke-RestMethod documentation. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.CodeGeneration.UnitLibrary` and `Curl.Extension.CodeGeneration.UnitTests` exist in `/Extensions/CodeGeneration/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--csharp <file>` and `--powershell <file>`, the way `--libcurl` writes C.
- [x] Unit tests check its wire behaviour against named sections of HttpClient and Invoke-RestMethod documentation, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 13): Plan: a transfer hook (ADR-1007), not a protocol handler, so it is registered in the option, description and transfer hook catalogues rather than ExtensionCatalogue.cs. Those three are shared list files (Tasks/SharedListFiles.txt) and were added to touches; ExtensionCatalogue.cs needs no line since the extension adds no scheme. Decisions in ADR-1128: before each transfer the hook rewrites each file with every request of the option group so far (one program per command line, as --libcurl); an unwritable file is logged and the transfer still runs; streamed bodies (-F file parts, -T), `-` for stdout and TLS/proxy/cookie options are left out.
- Wire behaviour criterion: tests check the generated text against named HttpClient (HttpClientHandler, HttpRequestMessage, TryAddWithoutValidation, HttpContentHeaders, ByteArrayContent) and Invoke-RestMethod (-Method/-CustomMethod, -Headers, -Body, -MaximumRedirection, -SkipHeaderValidation, -SkipHttpErrorCheck, about_Quoting_Rules) sections; no network. The hook test writes into a temporary directory it deletes, a unit test by the root rules.
- Quality: Measure-CodeQuality.ps1 -Library Curl.Extension.CodeGeneration.UnitLibrary: 100% line, 100% branch, 0 failing members (first run found three methods over complexity 10; split). 30 unit tests; dotnet build clean; fast tests green on Windows. Tests are platform-neutral (temp paths only; the unwritable-file case catches IOException and UnauthorizedAccessException, which differ by OS).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 5568185e 72614c69 42a58b93 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --csharp and --powershell write the command line's HTTP requests as HttpClient C# and Invoke-RestMethod PowerShell (ADR-1128)
