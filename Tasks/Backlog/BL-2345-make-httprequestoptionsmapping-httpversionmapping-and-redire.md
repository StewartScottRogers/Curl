---
id: BL-2345
title: Make HttpRequestOptionsMapping, HttpVersionMapping and RedirectPolicyMapping read EasyHttpOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2307]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2345 — Make HttpRequestOptionsMapping, HttpVersionMapping and RedirectPolicyMapping read EasyHttpOptions

## Goal

`HttpRequestOptionsMapping`, `HttpVersionMapping` and `RedirectPolicyMapping` (all in `Curl.Engine.UnitLibrary`), `TransferContextFactory`'s HTTP fields and the HTTP-version reads in `EngineComposition` and `OptionGroupConnectionSettings` read their HTTP settings from `EasyHttpOptions` instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2307 (ADR-0506 plan phase 1 step 4, ADR-0527), which built `EasyOptionsFromCommandLine.HttpOf`. Follow BL-2303's rules for retarget tasks; `Curl.Engine.UnitTests` already links `Curl.Console/EasyOptionsFromCommandLine.cs` (ADR-0526). Settings and their record members: `-H` + `--json` headers = `HttpHeaders` (the `--etag-compare` `If-None-Match` lines are still appended per transfer); `-A`/`-e`/`;auto` = `UserAgent`/`Referer`/`AutoReferer`; `--compressed` = `AcceptEncoding` `""`; `--raw` = both decodings off; `--tr-encoding`, `--ignore-content-length`, `--http0.9` (`Http09Allowed`), `--request-target`; `--expect100-timeout` = `Expect100Timeout` (default already 1 s, so `ContinueWaitOf` reads it directly); the version = `HttpVersion` (`None` for no option; `HttpVersionMapping` maps the record's enum, and `--no-alpn` is `SslEnableAlpn`); `-L`/`--follow` = `FollowLocation` `All`/`ObeyCode`; `--max-redirs` = `MaxRedirects`; `--post30x` = `PostRedirect`; the `-d` body = `PostFields` (null under `-G`). `-X`, `-f`, the authentication, proxy and network fields stay where their own groups' tasks put them (BL-2308, BL-2338, BL-2325, BL-2343).

## Acceptance criteria

- [ ] The classes named in Goal take `EasyHttpOptions` for their HTTP settings, not `CommandLineOptions`.
- [ ] Tests that built `CommandLineOptions` only to drive those classes build the record; tests through the whole command line stay.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

## Log

- 2026-10-15: Created.
