---
id: BL-4161
title: Extension: Get OAuth 2.0 tokens with device, client-credentials and PKCE flows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4211, BL-4212, BL-4213, BL-4209, BL-4210]
touches: [Curl.Extension.OAuth2.UnitLibrary, Curl.Extension.OAuth2.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.Extension.OAuth2.UnitLibrary/Options, Curl.Extension.OAuth2.UnitTests/Options]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4161 — Extension: Get OAuth 2.0 tokens with device, client-credentials and PKCE flows

## Goal

Curl provides `--oauth2-device`, `--oauth2-client-credentials`, `--oauth2-pkce`, with refresh and a token cache, as the extension `Curl.Extension.OAuth2.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 61 of 63. Specification: RFC 6749, RFC 7636, RFC 8628. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.OAuth2.UnitLibrary` and `Curl.Extension.OAuth2.UnitTests` exist in `/Extensions/OAuth2/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--oauth2-device`, `--oauth2-client-credentials`, `--oauth2-pkce`, with refresh and a token cache.
- [x] Unit tests check its wire behaviour against named sections of RFC 6749, RFC 7636, RFC 8628, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 2): Split for size before any code, following BL-4124. The first pass found that a transfer hook has no connector, no listener and only synchronous request headers (`ITransferHook.RequestHeadersFor`, the `Func<ExtensionOptionValues, ITransferHook?>` activators), so it cannot fetch a token before the transfer: filed BL-4211 for that hook. Filed BL-4212 (project and wire format: PKCE, token and device messages), BL-4213 (HTTP/1.1 client over IConnection, as ADR-1018 did for S3), BL-4209 (token cache and refresh) and BL-4210 (the three flows). This task keeps the options, the hook registration, the descriptions and the ADR. Touches widened to the option, description and transfer hook catalogue files (shared list files, one line each) and the library's Options folder, which this task's criteria need; no task in Doing on origin/work/dark-factory names those catalogue files beside its shared list line.

- 2026-10-29 (lane 8): Built `Options/` (ADR-1075): `OAuth2Options` (ten options, the description, `ActivatedBy`), `OAuth2Configuration` (reads the options, makes the flow), `OAuth2TransferHook` (gets the token through `TransferHookServices`, cache first, and adds `Authorization: Bearer` after curl's headers), `OAuth2FlowKind`, `OAuth2OptionException`. The library now references `Curl.Extension.Abstractions`; its tests reference `Curl.Extension.Composition` (as JsonRpc's do). The library's `CLAUDE.md` (inside the whole-project touches) updated.
- Criterion 1: OAuth2 adds no URL scheme, so `ExtensionCatalogue.cs` (protocol handler factories) gets no line, as BL-4106 recorded for `--jsonrpc`; it is registered in the option (10 lines), description and transfer hook catalogues. The full `dotnet build` (with `VerifyExtensionIsolation`) is clean.
- Defaults taken: each flow option takes the token endpoint; client secret always sent by HTTP Basic (RFC 6749 §2.3.1, which servers must support); redirect port 0; the cache is on in ADR-1034's default directory, `--oauth2-token-cache ""` turns it off; companions without a flow option do nothing; a wrong option, or a host giving no connector, fails with exit 2; a non-Bearer token fails with exit 8 (RFC 6749 §7.1).
- CURL_EXTENSIONS=0: `ExtensionCommandLineOptions.For` registers none of the ten options, so each is an unknown option (exit 2); without a flow option no hook is activated (unit tests in `OAuth2OptionsTests`).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.OAuth2.UnitLibrary,Curl.Extension.Composition.UnitLibrary`: 100% line and branch, 0 failing members, worst complexity 10 (OAuth2) and 6 (Composition). Fast tests: 69 projects green. The new tests use fakes and a temporary cache directory only, no platform paths, OS text or certificates, so they hold on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: de6a06ef 7a283adc 6cc92b01 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size and a missing hook: waits on BL-4211 (transfer hook connector, listener and async headers), BL-4212 (project and wire format), BL-4213 (HTTP client), BL-4209 (token cache and refresh) and BL-4210 (flows)
- 2026-10-29: Backlog -> Doing. Criteria at claim: de6a06ef 7a283adc 6cc92b01 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --oauth2-client-credentials, --oauth2-device and --oauth2-pkce get, cache and refresh an OAuth 2.0 token and send it as a Bearer token
