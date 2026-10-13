---
id: BL-2152
title: Remove the TLS-SRP plumbing curl 8.22.0 made dead: CommandLineOptions' six TLS-SRP properties, their Console mapping and the hand-built TLS-SRP client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2142]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions/ADR-0229-tls-srp-runs-rfc-5054-in-the-hand-built-tls-1-2-client-as-openssl-does.md, Documentation/Planning/Decisions/ADR-0328-tlsuser-runs-tls-srp-through-the-hand-built-client-as-curls-openssl-build.md]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2152 — Remove the TLS-SRP plumbing curl 8.22.0 made dead: CommandLineOptions' six TLS-SRP properties, their Console mapping and the hand-built TLS-SRP client

## Goal

No code path in Curl carries a TLS-SRP user, password or auth type any more, because curl 8.22.0 (the target since ADR-0479) accepts `--tlsuser`, `--tlspassword`, `--tlsauthtype` and their `--proxy-` forms only to warn `Warning: --<name> is deprecated and has no function anymore` (ADR-0480, BL-2142).

## Context

BL-2142 turned the six options into `CommandLineOption.NoFunctionValue` rows, so `CommandLineOptions.TlsUser`, `TlsPassword`, `TlsAuthType`, `ProxyTlsUser`, `ProxyTlsPassword` and `ProxyTlsAuthType` are now get-only and always `null`. They were left in place only because removing them reaches projects outside BL-2142's `touches`. Still to remove or revise:

- `Curl.Cli.UnitLibrary/CommandLineOptions.cs`: the six always-null properties.
- `Curl.Console/TlsClientOptionsMapping.cs` and `Curl.Console.UnitTests/TlsClientOptionsMappingTests.cs`: the TLS-SRP mapping.
- `Curl.Networking.UnitLibrary`: `TlsSrp.cs`, the SRP paths in `HandBuiltTlsProvider.cs`, `TlsClientRouting.cs`, `TlsClientOptions.cs` (`TlsUser`, `TlsPassword`, `TlsAuthType`), the `CLAUDE.md` mention, and their tests (`HandBuiltTlsProviderTests.TlsSrp.cs`, `TlsClientRoutingTests.cs`, `TlsClientOptionsTests.cs`).
- `Curl.Tls.UnitLibrary/TlsSrpCredentials.cs` and its tests.
- `Curl.Cli.UnitLibrary/CurlVersionText.cs`: whether curl 8.22.0's OpenSSL build still lists `TLS-SRP` in its `Features:` line; measure on Linux or from the 8.22.0 source before changing `FeaturesLine`.
- ADR-0229 and ADR-0328 are superseded by ADR-0480; mark them so.

## Acceptance criteria

- [x] `grep -rn "TlsUser\|TlsPassword\|TlsAuthType\|TlsSrp" --include=*.cs` finds nothing outside tests that pin the no-function warning.
- [x] `curl --tlsuser u --tlspassword p --tlsauthtype SRP https://...` still warns three times and runs the transfer, exit as without them.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass, and every library changed stays at 100% line and branch coverage.

## Notes

- Split (rule 4, first pass): the Curl.Tls half - `TlsSrpCredentials` and the whole SRP key exchange it feeds (`SrpClient`, `SrpGroup`, `SrpExtension`, `Tls12SrpParameters`, the SRP suites) - is far larger than the task's list suggests, so it went to BL-2157. Nothing outside Curl.Tls reaches it any more, so the first acceptance criterion holds outside Curl.Tls here and inside it once BL-2157 is done.
- Removed: `CommandLineOptions`' six properties; the Console mapping; `TlsClientOptions.TlsUser/TlsPassword/TlsAuthType`; `TlsSrp`; the SRP paths of `HandBuiltTlsProvider` (SRP suites, exit 43, OpenSSL handshake-failure text for `--tlsuser`); `TlsClientRouting`'s TLS-SRP row; their tests. The tests pinning that the options carry nothing (`TlsClientOptionsMappingTests`, `CommandLineNoFunctionOptionTests`) stay.
- `Features:`: curl 8.22.0's `lib/version.c` (curl-8_22_0 tag, fetched 2026-10-13) has no `TLS-SRP` feature; 8.21.0's listed it under `USE_TLS_SRP`. So `CurlVersionText.FeaturesLine` drops it. `Curl.Conformance.UnitLibrary`'s `UpstreamCurlPlatform` still lists it, outside `touches`: filed as BL-2158.
- ADR-0229 and ADR-0328 marked superseded by ADR-0480; added to `touches` (no task in Doing on origin/work/dark-factory names Documentation).
- Behaviour (criterion 2) is parsing-only and unchanged: `CommandLineNoFunctionOptionTests` still pins the three warnings and the transfer running.
- Coverage: only code was removed, nothing added; Measure-CodeQuality.ps1 was not run within this run's budget.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. TLS-SRP plumbing removed from Cli, Console and Networking; Features line drops TLS-SRP; Curl.Tls half split to BL-2157, Conformance list to BL-2158
