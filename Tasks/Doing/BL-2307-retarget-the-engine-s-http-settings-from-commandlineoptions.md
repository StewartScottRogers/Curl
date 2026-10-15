---
id: BL-2307
title: Retarget the engine's HTTP settings from CommandLineOptions to EasyHttpOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2306, BL-2299]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Http, Curl.Api.UnitTests/Options/Http]
requirement: none
created: 2026-10-15
completed:
---
# BL-2307 — Retarget the engine's HTTP settings from CommandLineOptions to EasyHttpOptions

## Goal

The engine reads its HTTP settings - headers, methods, bodies and MIME, versions, redirects, cookies, HSTS, alt-svc, decoding - from `EasyHttpOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds that record; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2306 and uses the rules for every retarget task written in BL-2303's Context. This is the largest group; if it cannot finish in one run, move it to `Blocked` naming the split (for example headers and bodies / redirects, cookies, HSTS and alt-svc) and have the follow-up filed, per the task-board skill. Engine classes reading HTTP settings (as of 2026-10-15): `HttpRequestOptionsMapping`, `HttpVersionMapping`, `MultipartFormPartMapping`, `RedirectPolicyMapping`, `CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile`, the HSTS and alt-svc steps of `TransferPerformer`, `TransferContextFactory`'s HTTP fields. The body read from `-d @file` or `-F` files is read by the tool (as `config2setopts` does) and handed over as bytes or a MIME body.

## Acceptance criteria

- [ ] The engine classes named in Context take `EasyHttpOptions`, not `CommandLineOptions`, for their HTTP settings.
- [ ] `EasyOptionsFromCommandLine` maps every HTTP command-line option (`-H`, `-A`, `-e`, `--compressed`, `--tr-encoding`, `--raw`, `-L`, `--max-redirs`, `--post301/302/303`, `-d` and its variants, `--json`, `-F`, `--form-string`, `--form-escape`, `-G`, `-I`, `-X` for HTTP, `--http0.9` to `--http3-only`, `--ignore-content-length`, `--expect100-timeout`, `--request-target`, `-b`, `-c`, `-j`, `--alt-svc`, `--hsts`, `--no-alpn`, `--no-npn`, `--path-as-is` where HTTP-specific, and the rest of the group), each with a test.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
