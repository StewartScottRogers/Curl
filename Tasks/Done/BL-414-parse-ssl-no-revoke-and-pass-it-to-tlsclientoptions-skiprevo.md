---
id: BL-414
title: Parse --ssl-no-revoke and pass it to TlsClientOptions.SkipRevocationCheck
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-414 — Parse --ssl-no-revoke and pass it to TlsClientOptions.SkipRevocationCheck

## Goal

`curl --ssl-no-revoke --cacert root.pem https://host/` parses, and in the Schannel build a chain from a private CA with no revocation endpoint then succeeds, as curl 8.21.0's Schannel build does.

## Context

- BL-368 made the Schannel build check revocation for a `--cacert` chain (ADR-0086) and added `TlsClientOptions.SkipRevocationCheck` in `Curl.Networking.UnitLibrary` to turn it off. Nothing sets it yet.
- `--ssl-no-revoke` is already in `Curl.Cli.UnitLibrary/CurlHelpTable.cs` but not parsed. curl 8.21.0 accepts it in every build; the OpenSSL build ignores it.
- `Curl.Console/TlsClientOptionsMapping.cs` builds `TlsClientOptions` from the parsed options; `Curl.Console.UnitTests/TlsClientOptionsMappingTests.cs` pins the mapping.
- Measured in BL-150: with `--cacert root.pem --ssl-no-revoke`, a leaf signed by that root succeeds (exit 0); without the option it is exit 60 `schannel: the revocation status is unknown`.

## Acceptance criteria

- [x] A named test in `Curl.Cli.UnitTests` shows `--ssl-no-revoke` parses and sets the parsed option; one without it leaves it unset.
- [x] A named test in `Curl.Console.UnitTests` shows the mapping sets `TlsClientOptions.SkipRevocationCheck` from it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-368.
- Delivered directly rather than through the full /feature agent stages: a one-flag parse plus a one-argument mapping, with the TLS behaviour already built and tested in BL-368 (ADR-0086); no new design decision, so no ADR.
- `--ssl-no-revoke` is a `NegatableFlag` (the alias table already accepts `--no-ssl-no-revoke`) setting `CommandLineOptions.SkipRevocationCheck`, named after `TlsClientOptions.SkipRevocationCheck` so one concept has one name. Accepted in every build as curl is; the OpenSSL build ignores it in `SslStreamTlsProvider`.
- Tests: `CommandLineTlsOptionTests.Parse_SslNoRevoke_SetsSkipRevocationCheck`, `Parse_SslNoRevokeThenNoSslNoRevoke_ChecksRevocation`, `Parse_NoTlsOptions_LeavesThemNotGiven` (unset); `TlsClientOptionsMappingTests.FromCommandLine_SslNoRevoke_SetsSkipRevocationCheckOnly`, `FromCommandLine_SslNoRevokeWithCaCertificateFile_SetsBoth`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --ssl-no-revoke parses and sets TlsClientOptions.SkipRevocationCheck, so the Schannel build accepts a private CA chain with no revocation endpoint
