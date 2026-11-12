---
id: BL-5050
title: Refuse --csharp - and --powershell - instead of writing a file named -
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary/CodeFile.cs, Curl.Extension.CodeGeneration.UnitLibrary/EmptyPathTransferHook.cs, Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationExtension.cs, Curl.Extension.CodeGeneration.UnitTests/EmptyPathTransferHookTests.cs, Curl.Extension.CodeGeneration.UnitTests/CodeGenerationExtensionTests.cs, Documentation/Planning/Decisions/ADR-1138-code-generation-extension-writes-httpclient-csharp-and-invoke-restmethod-powershell.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-5050 — Refuse --csharp - and --powershell - instead of writing a file named -

## Goal

`curl --csharp -` and `curl --powershell -` no longer create a file named `-`: they exit 2 with a message naming the option (or write the code to stdout, as decided in the ADR).

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-042 (Low, Confirmed).

**Repro.** `curl -sS --csharp - http://127.0.0.1:48000/plain; ls -- -`.

**Expected.** The help says code is written "the way --libcurl writes C", and `--libcurl -` writes to stdout. ADR-1138 leaves out `-` on purpose, because the code would mix with the body. Either way the user must be told: refuse with exit 2, or write to stdout.

**Actual.** curl prints the body and exits 0, and creates a 560-byte file literally named `-` with no warning.

**Where to start.** `Curl.Extension.CodeGeneration.UnitLibrary/CodeFile.cs` and `EmptyPathTransferHook.cs` (an empty path is refused there with `CurlExitCode.FailedInit`, but `-` is not). Decision to take under Stewart's delegation and record in ADR-1138: the review allows either; ADR-1138 already excludes `-` on purpose, so refusing with exit 2 is the matching choice.

## Acceptance criteria

- [ ] `Curl.Extension.CodeGeneration.UnitTests/EmptyPathTransferHookTests.cs` (or `CodeGenerationExtensionTests.cs`) has a test that `--csharp -` gives exit 2 (`CurlExitCode.FailedInit`) with a message naming `--csharp`, and that the wrapped handler is not called and no file is created.
- [ ] The same tests for `--powershell -`, with a message naming `--powershell`.
- [ ] Regression: `--csharp <path>` and `--powershell <path>` with a real path still write the generated code, and the empty-path refusal is unchanged.
- [ ] ADR-1138 (`Documentation/Planning/Decisions/ADR-1138-code-generation-extension-writes-httpclient-csharp-and-invoke-restmethod-powershell.md`) states that `-` is refused with exit 2 and why.
- [ ] `--ai-help` stays right: the option descriptions in `Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationExtension.cs` say `-` is not accepted, and any test that pins the help text is updated.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Unit tests use a fake file system or the hook's scripted context; none touches the real disk or a server.

## Log

- 2026-11-12: Created.
