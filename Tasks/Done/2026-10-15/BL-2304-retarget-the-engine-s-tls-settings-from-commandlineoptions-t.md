---
id: BL-2304
title: Retarget the engine's TLS settings from CommandLineOptions to EasyTlsOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2303, BL-2296]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Networking.UnitLibrary/ClientCertificateArgument.cs, Curl.Api.UnitLibrary/Options/Tls, Curl.Api.UnitTests/Options/Tls]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2304 — Retarget the engine's TLS settings from CommandLineOptions to EasyTlsOptions

## Goal

The engine reads its TLS settings (origin, HTTPS proxy and DoH) from `EasyTlsOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds that record; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2303 and uses the rules for every retarget task written in its Context. Engine classes reading TLS settings (as of 2026-10-15): `TlsClientOptionsMapping`, `TlsBuildInformation`, `EngineComposition.CreateTlsProvider` and the TLS session cache wiring, `TransferContextFactory`'s TLS fields. Command-line options: `--cacert`, `--capath`, `--cert`, `--cert-type`, `--key`, `--key-type`, `--pass`, `--ciphers`, `--tls13-ciphers`, `--curves`, `--sigalgs`, `--tlsv1*`, `--tls-max`, `--sslv*`, `-k`, `--doh-insecure`, `--doh-cert-status`, `--cert-status`, `--pinnedpubkey`, `--crlfile`, `--ssl-no-revoke`, `--ssl-revoke-best-effort`, `--ssl-allow-beast`, `--ssl-auto-client-cert`, `--ca-native`, `--no-sessionid`, `--false-start`, `--ech`, `--engine`, the `--proxy-*` TLS twins, `--tls-earlydata`, `--ssl-sessions` and the rest of the group.

## Acceptance criteria

- [x] ~~The engine classes named in Context take `EasyTlsOptions`, not `CommandLineOptions`, for their TLS settings.~~ — split out to BL-2324 (see Notes).
- [x] `EasyOptionsFromCommandLine` maps every TLS command-line option named in Context, each with a test.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- 2026-10-15, split (rule 4, before any code), as BL-2303 split: the engine cannot read `EasyTlsOptions` until BL-2323 gives `Curl.Engine.UnitLibrary` its reference to `Curl.Api.UnitLibrary`, and the retarget also changes `Curl.Networking`'s `ClientCertificateLoader` (the record carries `-E` already split). This task delivers `EasyOptionsFromCommandLine.TlsOf`, the record's ECH gap and ADR-0514; BL-2324 (depends on this and BL-2323) retargets the engine's TLS classes, and BL-2310 now also depends on BL-2324.
- Decided (ADR-0514, by Claude under Stewart's delegation): `EasyTlsOptions` gains `EncryptedClientHelloPublicName` and `EncryptedClientHelloConfigList` (libcurl keeps `pn:` and `ecl:` beside the mode); `-E` is split in the mapping with `Curl.Networking`'s `ClientCertificateArgument`, made public for it (only the Windows build keeps a drive colon; `--pass` outranks the value's pass phrase; `pkcs11:` gives type `ENG`).
- Touches: added `Curl.Networking.UnitLibrary/ClientCertificateArgument.cs` (rule 3; no task in Doing on `origin/work/dark-factory` names it) for the one-word `internal` to `public` change, so one splitter serves both. Visibility only, so its coverage and complexity are unchanged and it was not measured.
- Where the mapping disagrees with `--libcurl` and the command line's behaviour wins: no minimum TLS version is `SslVersion.Default` (`--libcurl` writes `CURL_SSLVERSION_TLSv1_2`; the engine uses the TLS library's own). `--capath` reaches the proxy without `--proxy-capath`, as the engine does. `-w` sets `GatherCertificateInfo` as `--libcurl` writes `CURLOPT_CERTINFO`. `--engine` sets `Engine` only. `--sslv2`, `--sslv3` and `--false-start` set nothing; `--ssl-sessions` stays the tool's (`TlsSessionFileLines`); `--no-alpn` is the HTTP group's `CURLOPT_SSL_ENABLE_ALPN`.
- `--ai-help` unaffected (no option changes).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Api.UnitLibrary` - first run had `SslVersionOf` and `SslVersionMaximumOf` at complexity 12 (switch on a nullable enum), replaced with frozen-dictionary lookups; second run 100% line and branch, 0 failing. Fast tests: all green (Console 2423 passed, Api 421, Conformance 3112, Engine 648).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyOptionsFromCommandLine.TlsOf maps every TLS-group option onto EasyTlsOptions (ADR-0514); engine retarget split to BL-2324
