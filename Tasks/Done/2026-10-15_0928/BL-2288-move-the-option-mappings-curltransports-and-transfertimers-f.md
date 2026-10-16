---
id: BL-2288
title: Move the option mappings, CurlTransports and TransferTimers from Curl.Console to Curl.Engine.UnitLibrary
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-2287]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2288 — Move the option mappings, CurlTransports and TransferTimers from Curl.Console to Curl.Engine.UnitLibrary

## Goal

The option mappings (command-line options to protocol option records), `CurlTransports`, `TransferTimers` and their neighbours live in `Curl.Engine.UnitLibrary` (namespace `Curl.Engine`), with their tests in `Curl.Engine.UnitTests`.

## Context

ADR-0506, plan phase 1 step 3; follows BL-2287, same rules (mechanical move, a file that references a tool-side type stays with a reason in Notes, `CLAUDE.md` paragraphs move with the code). They still read `CommandLineOptions` after the move; BL-2303 to BL-2309 retarget them to the seam's option groups. Files (from `Curl.Console`, as of 2026-10-15): `HttpRequestOptionsMapping`, `HttpVersionMapping`, `MailRequestOptionsMapping`, `MultipartFormPartMapping`, `RedirectPolicyMapping`, `SshOptionsMapping`, `TlsClientOptionsMapping`, `OptionGroupConnectionSettings`, `TlsBuildInformation`, `TransferProxySelection`, `TransferTimers`, `UnusableDohUrlResolver`, `CurlTransports`. `RetryPolicyMapping` stays: `--retry` is the curl tool's.

## Acceptance criteria

- [x] None of the files named in Context (other than any kept with a reason in Notes) remains in `Curl.Console`; each is in `Curl.Engine.UnitLibrary` in namespace `Curl.Engine`; `RetryPolicyMapping` is still in `Curl.Console`.
- [x] Their tests are in `Curl.Engine.UnitTests` with the same test names and assertions; none was deleted.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the fast-test count is not lower than before).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [x] `Curl.Console/CLAUDE.md` and `Curl.Engine.UnitLibrary/CLAUDE.md` each describe only the code in their own project.

## Notes

- Kept in `Curl.Console` with a reason: `OptionGroupConnectionSettings` (calls `CurlComposition`). Moved: the other 12 files.
- Tests that drive `CurlComposition` stayed in `Curl.Console.UnitTests` (namespace `Curl.Console`, `using Curl.Engine`), names unchanged: `CurlTransportsTests`, `HttpVersionMappingTests`, `TransferProxySelectionPreProxyTests`. `TlsBuildInformationTests`, `TlsClientOptionsMappingTests`, `MultipartFormPartMappingTests`, `RedirectPolicyMappingTests` and `TransferTimersTests` moved; `TlsClientOptionsMappingTests` links `OpenSslBuildParser` from `Curl.Cli.UnitTests`.
- `<see cref>` to `CurlComposition` members in two moved doc comments became `<c>` text.
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 0 failing members. Fast tests green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Moved 12 of 13 files to Curl.Engine; build clean, fast tests green, quality 0 failing
