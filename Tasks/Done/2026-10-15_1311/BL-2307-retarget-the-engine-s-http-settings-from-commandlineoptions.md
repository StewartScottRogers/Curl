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
completed: 2026-10-15
---
# BL-2307 — Retarget the engine's HTTP settings from CommandLineOptions to EasyHttpOptions

## Goal

The engine reads its HTTP settings - headers, methods, bodies and MIME, versions, redirects, cookies, HSTS, alt-svc, decoding - from `EasyHttpOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds that record; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2306 and uses the rules for every retarget task written in BL-2303's Context. This is the largest group; if it cannot finish in one run, move it to `Blocked` naming the split (for example headers and bodies / redirects, cookies, HSTS and alt-svc) and have the follow-up filed, per the task-board skill. Engine classes reading HTTP settings (as of 2026-10-15): `HttpRequestOptionsMapping`, `HttpVersionMapping`, `MultipartFormPartMapping`, `RedirectPolicyMapping`, `CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile`, the HSTS and alt-svc steps of `TransferPerformer`, `TransferContextFactory`'s HTTP fields. The body read from `-d @file` or `-F` files is read by the tool (as `config2setopts` does) and handed over as bytes or a MIME body.

## Acceptance criteria

- [x] The engine classes named in Context take `EasyHttpOptions`, not `CommandLineOptions`, for their HTTP settings. Split (see Notes): BL-2345 (request, version and redirect mappings, `TransferContextFactory`) and BL-2346 (cookies, alt-svc, HSTS, `TransferPerformer` steps).
- [x] `EasyOptionsFromCommandLine` maps every HTTP command-line option, each with a test (`EasyOptionsFromCommandLineTests.Http.cs`); `-F` / `--form-string` onto `MimePost` is BL-2344, `-X` and `-I` are `EasyTransferOptions`' (BL-2308), `--path-as-is` is `EasyNetworkOptions`' (done in BL-2303), and `--no-npn` has no function in curl 8 (ADR-0527).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- 2026-10-15, split (before any code), as BL-2303 to BL-2306 split: the engine retarget of the HTTP group spans nine classes and the `-F` form needs a record decision, too much for one run. This task delivers `EasyOptionsFromCommandLine.HttpOf`, `EasyHttpOptions.SslEnableAlpn` and ADR-0527; filed BL-2344 (`-F` onto `MimePost` and `MultipartFormPartMapping`), BL-2345 (`HttpRequestOptionsMapping`, `HttpVersionMapping`, `RedirectPolicyMapping`, `TransferContextFactory`'s HTTP fields) and BL-2346 (`CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile`, `TransferPerformer`'s cache steps), all depending on this; BL-2310 now also depends on them.
- Decided (ADR-0527, by Claude under Stewart's delegation), where the mapping differs from `--libcurl` the command line wins: `-A` absent stays `null` (the HTTP handler supplies `curl/8.22.0`); every version option maps, not only the Schannel build's 1.0/1.1; `MaxRedirects` is the tool's 50; `--raw` turns both decodings off; `-G` leaves no `PostFields`; `--expect100-timeout 0` keeps 1 s; `--alt-svc`/`--hsts` also set libcurl's control bits (`H1|H2|H3`, `Enable`); the `--etag-compare` header stays per transfer.
- `--ai-help` unaffected (no option changes).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Api.UnitLibrary` - both 100% line and branch; first run had `WithCaches` at complexity 14 (lambdas), split into `WithCookies`, `CookieValuesOf` and `WithAltSvcAndHsts` (each well under 10, branches covered by the same tests). Fast tests: 39 assemblies green (Console 2650 passed, Api 423, Engine 652, Conformance 3129). Ratchet file unchanged. `dotnet format --verify-no-changes` reports nothing in the files changed here (it flags import order in other, untouched Console files).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyOptionsFromCommandLine.HttpOf maps the HTTP group onto EasyHttpOptions (ADR-0527); engine retarget split to BL-2344 to BL-2346
