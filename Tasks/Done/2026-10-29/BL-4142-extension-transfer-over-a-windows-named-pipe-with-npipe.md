---
id: BL-4142
title: Extension: Transfer over a Windows named pipe with --npipe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4095]
touches: [Curl.Extension.NamedPipe.UnitLibrary, Curl.Extension.NamedPipe.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4142 — Extension: Transfer over a Windows named pipe with --npipe

## Goal

Curl provides `--npipe <\\.\pipe\name>`, the Windows counterpart of `--unix-socket`, as the extension `Curl.Extension.NamedPipe.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 41 of 63. Specification: Windows named pipes (System.IO.Pipes). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.NamedPipe.UnitLibrary` and `Curl.Extension.NamedPipe.UnitTests` exist in `/Extensions/NamedPipe/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--npipe <\\.\pipe\name>`, the Windows counterpart of `--unix-socket`.
- [x] Unit tests check its wire behaviour against named sections of Windows named pipes (System.IO.Pipes), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 6): Plan: a connection-hook extension (ADR-1010), not a scheme handler. `NamedPipeName` parses "Pipe Names"' `\\server\pipe\name`; `NamedPipeConnector` opens the pipe through `INamedPipeOpener` for every connect, whatever the URL's host and port; `SystemNamedPipeOpener` is the excluded `NamedPipeClientStream` adapter (ADR-0083). Decisions in ADR-1021.
- Registration: `ExtensionCatalogue.cs` holds protocol handler factories only, and this extension adds no scheme, so its "one line" is in `ExtensionConnectionHookCatalogue.cs` (its activator), with its option and description one line each in `ExtensionOptionCatalogue.cs` and `ExtensionDescriptionCatalogue.cs`, as ADR-1006, ADR-1008 and ADR-1010 require. Those three shared list files (merge=union, in `Tasks/SharedListFiles.txt`) were added to `touches`; they are exempt from the overlap check.
- Criterion 4: `NamedPipeExtensionTests` checks that `ExtensionCommandLineOptions.For` leaves `npipe` out under `CURL_EXTENSIONS=0` (so the parser refuses it as unknown) and that the connection-hook catalogue keeps curl's connector without the option. Measured on the built curl.exe: `curl: option --npipe: is unknown`, exit 2; a missing pipe gives exit 7; a value that is not a pipe name gives exit 2; a live `NamedPipeServerStream` in PowerShell received `GET /_ping HTTP/1.1` and curl printed its 5-byte body, exit 0. `curl --extensions` and `curl --ai-help extensions` list `npipe`.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.NamedPipe.UnitLibrary,Curl.Extension.Composition.UnitLibrary` found `NamedPipeName.TryParse` at complexity 12; split into `TrySplit`, then the NamedPipe re-run gave 0 failing members, 100/100; Composition had 0 failing members.
- Platform: the tests use no OS pipe, path or error text, so they are platform-neutral; CI on Linux and macOS is covered by that.
- Left out: TLS over the pipe (exit 4) and HTTP/3; TLS is filed as BL-4206.
- Fast tests: 53 test projects passed, 0 failed; the new project has 34 tests.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 13bde9f4 0ede1753 14e288ad e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. curl --npipe \.\pipe\name runs a transfer over a Windows named pipe, as an extension
