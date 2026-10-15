---
id: BL-2304
title: Retarget the engine's TLS settings from CommandLineOptions to EasyTlsOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2303, BL-2296]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Tls, Curl.Api.UnitTests/Options/Tls]
requirement: none
created: 2026-10-15
completed:
---
# BL-2304 — Retarget the engine's TLS settings from CommandLineOptions to EasyTlsOptions

## Goal

The engine reads its TLS settings (origin, HTTPS proxy and DoH) from `EasyTlsOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds that record; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2303 and uses the rules for every retarget task written in its Context. Engine classes reading TLS settings (as of 2026-10-15): `TlsClientOptionsMapping`, `TlsBuildInformation`, `EngineComposition.CreateTlsProvider` and the TLS session cache wiring, `TransferContextFactory`'s TLS fields. Command-line options: `--cacert`, `--capath`, `--cert`, `--cert-type`, `--key`, `--key-type`, `--pass`, `--ciphers`, `--tls13-ciphers`, `--curves`, `--sigalgs`, `--tlsv1*`, `--tls-max`, `--sslv*`, `-k`, `--doh-insecure`, `--doh-cert-status`, `--cert-status`, `--pinnedpubkey`, `--crlfile`, `--ssl-no-revoke`, `--ssl-revoke-best-effort`, `--ssl-allow-beast`, `--ssl-auto-client-cert`, `--ca-native`, `--no-sessionid`, `--false-start`, `--ech`, `--engine`, the `--proxy-*` TLS twins, `--tls-earlydata`, `--ssl-sessions` and the rest of the group.

## Acceptance criteria

- [ ] The engine classes named in Context take `EasyTlsOptions`, not `CommandLineOptions`, for their TLS settings.
- [ ] `EasyOptionsFromCommandLine` maps every TLS command-line option named in Context, each with a test.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
