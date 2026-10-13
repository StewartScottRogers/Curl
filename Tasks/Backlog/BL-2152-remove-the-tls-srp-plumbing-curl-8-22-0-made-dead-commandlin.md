---
id: BL-2152
title: Remove the TLS-SRP plumbing curl 8.22.0 made dead: CommandLineOptions' six TLS-SRP properties, their Console mapping and the hand-built TLS-SRP client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2142]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-13
completed:
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

- [ ] `grep -rn "TlsUser\|TlsPassword\|TlsAuthType\|TlsSrp" --include=*.cs` finds nothing outside tests that pin the no-function warning.
- [ ] `curl --tlsuser u --tlspassword p --tlsauthtype SRP https://...` still warns three times and runs the transfer, exit as without them.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass, and every library changed stays at 100% line and branch coverage.

## Notes

## Log

- 2026-10-13: Created.
