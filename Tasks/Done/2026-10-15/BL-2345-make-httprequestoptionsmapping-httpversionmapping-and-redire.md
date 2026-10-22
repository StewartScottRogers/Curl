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
completed: 2026-10-15
---
# BL-2345 — Make HttpRequestOptionsMapping, HttpVersionMapping and RedirectPolicyMapping read EasyHttpOptions

## Goal

`HttpRequestOptionsMapping`, `HttpVersionMapping` and `RedirectPolicyMapping` (all in `Curl.Engine.UnitLibrary`), `TransferContextFactory`'s HTTP fields and the HTTP-version reads in `EngineComposition` and `OptionGroupConnectionSettings` read their HTTP settings from `EasyHttpOptions` instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2307 (ADR-0506 plan phase 1 step 4, ADR-0527), which built `EasyOptionsFromCommandLine.HttpOf`. Follow BL-2303's rules for retarget tasks; `Curl.Engine.UnitTests` already links `Curl.Console/EasyOptionsFromCommandLine.cs` (ADR-0526). Settings and their record members: `-H` + `--json` headers = `HttpHeaders` (the `--etag-compare` `If-None-Match` lines are still appended per transfer); `-A`/`-e`/`;auto` = `UserAgent`/`Referer`/`AutoReferer`; `--compressed` = `AcceptEncoding` `""`; `--raw` = both decodings off; `--tr-encoding`, `--ignore-content-length`, `--http0.9` (`Http09Allowed`), `--request-target`; `--expect100-timeout` = `Expect100Timeout` (default already 1 s, so `ContinueWaitOf` reads it directly); the version = `HttpVersion` (`None` for no option; `HttpVersionMapping` maps the record's enum, and `--no-alpn` is `SslEnableAlpn`); `-L`/`--follow` = `FollowLocation` `All`/`ObeyCode`; `--max-redirs` = `MaxRedirects`; `--post30x` = `PostRedirect`; the `-d` body = `PostFields` (null under `-G`). `-X`, `-f`, the authentication, proxy and network fields stay where their own groups' tasks put them (BL-2308, BL-2338, BL-2325, BL-2343).

## Acceptance criteria

- [x] The classes named in Goal take `EasyHttpOptions` for their HTTP settings, not `CommandLineOptions`.
- [x] Tests that built `CommandLineOptions` only to drive those classes build the record; tests through the whole command line stay.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

- Split before any code (rule 4): threading `EasyHttpOptions` through `CreateTransports`, `CreateTcpConnector` and `CreatePoolingConnector` reaches about 120 test call sites, so the `EngineComposition` and `OptionGroupConnectionSettings` HTTP-version reads went to BL-2350. Until it and BL-2346 land, `HttpVersionMapping.FromRequested` bridges `CommandLineOptions.HttpVersion` for them and `AltSvcTransferCache`.
- Decisions (ADR-0532): `Compressed` = a set `AcceptEncoding`; `Raw` = both decodings off; `ContinueWait` reads `Expect100Timeout` directly (`ContinueWaitOf` removed, `HttpOf` already applies the 1 s default); `Http2Tls` asks for HTTP/1.1 and offers `h2,http/1.1`; `TransferContext.PostData` still reads `CommandLineOptions.PostData` (non-HTTP handlers read it under `-G` too).
- `TransferContextFactory.Create` takes the record as its second parameter; the runner builds it with `HttpOf` per attempt. New `HttpRequestOptionsMappingTests` and a record-built `RedirectPolicyMappingTests` case drive the mappings with a record; whole-command-line tests stay.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` found one gap, `FromRequested`'s out-of-range branch, closed with a `(RequestedHttpVersion)99` row; fast tests green in all 39 projects; `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. HttpRequestOptionsMapping, HttpVersionMapping, RedirectPolicyMapping and TransferContextFactory's HTTP fields read EasyHttpOptions; EngineComposition's reads split to BL-2350
