---
id: BL-2921
title: Port upstream unit programs 3303 and 3304: mTLS key, key type and cert type in connection reuse and the TLS session peer key
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Tls, Curl.Conformance.UnitProgram.UnitTests/Dns/Tls, Curl.Networking.UnitLibrary/TlsSessionCache.cs, Curl.Networking.UnitTests/TlsSessionCacheTests.cs, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2921 — Port upstream unit programs 3303 and 3304: mTLS key, key type and cert type in connection reuse and the TLS session peer key

## Goal

Upstream cases 3303 (connection reuse distinguishes the mTLS key, key password, key type and cert type) and 3304 (the TLS session cache peer key includes the key, key type and cert type, case-insensitively for the types, and never the key password) run as ported unit programs and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2847 (too big for one run). C sources: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit3303.c (`Curl_ssl_conn_config_match`: identical config matches; a different `key_passwd`, `key`, `key_type` or `cert_type` does not; restored matches again) and https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit3304.c (`Curl_ssl_peer_key_make`: same config same key; `key_passwd` does not change it; a different key, key type or cert type does; `PEM`/`pem` for either type does not). Curl's counterparts: `Curl.Networking.UnitLibrary/TlsSessionCache.cs` (`PeerKey(host, port, TlsClientOptions)`) for 3304; for 3303, the code that decides whether a pooled connection may be reused for a transfer's TLS client options (find it in `Curl.Engine.UnitLibrary`; narrow `touches` to its files per ADR-0694). Curl has no `data->set`: port the observable part (reuse yes/no, peer key equal/different) and say so in each doc comment. One `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Tls/`, each registered by one added line in `UnitPrograms.Dns.cs` (union-merged); tests in `Curl.Conformance.UnitProgram.UnitTests/Dns/Tls/`. Engine gives this library no `InternalsVisibleTo` (BL-2889 notes): if the reuse decision is internal, use a public seam or file the seam as its own task first.

## Acceptance criteria

- [ ] `unit3303` and `unit3304` are ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Tls/` with checks matching the C programs row for row (named in each doc comment), registered in `UnitPrograms.Dns.cs`, each with a test.
- [ ] Cases 3303 and 3304 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 9b883e1f 7e874ed9 aca7e1a2 dd1cc079.
