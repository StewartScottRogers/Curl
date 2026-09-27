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
completed:
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

- [ ] A named test in `Curl.Cli.UnitTests` shows `--ssl-no-revoke` parses and sets the parsed option; one without it leaves it unset.
- [ ] A named test in `Curl.Console.UnitTests` shows the mapping sets `TlsClientOptions.SkipRevocationCheck` from it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-368.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
