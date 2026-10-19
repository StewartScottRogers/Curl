---
id: BL-2785
title: Import and export one packed TLS session through TlsSessionCache, as curl_easy_ssls_import and _export need
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsSessionCache.cs, Curl.Networking.UnitLibrary/TlsSessionPacking.cs, Curl.Networking.UnitTests/TlsSessionCacheTests.cs, Curl.Networking.UnitTests/TlsSessionPackingTests.cs]
requirement: none
created: 2026-10-18
completed:
---
# BL-2785 — Import and export one packed TLS session through TlsSessionCache, as curl_easy_ssls_import and _export need

## Goal

`TlsSessionCache` imports one packed session (curl 8.22.0's `vtls_spack.c` format, `TlsSessionPacking`) under a peer key or a salt-and-hmac, and lists every held session for export, so the engine can implement `curl_easy_ssls_import` and `curl_easy_ssls_export`.

## Context

Found by BL-2669. Today the cache only reads and writes a whole `--ssl-sessions` file (`Import(text, fileName)`, `Export()`). libcurl's `curl_easy_ssls_import(easy, session_key, shmac, shmac_len, sdata, sdata_len)` (curl 8.22.0 `lib/vtls/vtls_scache.c` `Curl_ssl_session_import`) adds one packed session: under `session_key` when given, else under the 64-byte salt-and-hmac (`CURLE_BAD_FUNCTION_ARGUMENT` when neither, or a hmac not 64 bytes), answering `CURLE_BAD_FUNCTION_ARGUMENT` for bytes `Curl_ssl_session_unpack` refuses. `curl_easy_ssls_export` calls back once per held session with the key's salt and hmac, the packed bytes, `valid_until`, the IETF TLS id, ALPN and max early data. lib1678 imports, 64 times under `example.test:443`, a packet holding a 4096-byte ticket, a 127-byte QUIC transport parameter block, a 10-byte ALPN, then a second one-byte ticket, ALPN and QUIC block (later tags replace earlier ones in upstream's unpack); every import must answer OK. `ImportLine` already maps unpack and hmac failures to curl's codes 26 and 43; reuse that logic.

## Acceptance criteria

- [ ] `TlsSessionCache` has a public method importing one packed session under a peer key or a 64-byte salt-and-hmac, answering curl's code for each refusal (bad packing, neither key nor 64-byte hmac), with unit tests in `Curl.Networking.UnitTests/TlsSessionCacheTests.cs`.
- [ ] `TlsSessionPacking.Unpack` accepts lib1678's packet (repeated ticket, ALPN and QUIC parameter tags, the later value kept) with a unit test pinning it.
- [ ] `TlsSessionCache` has a public method listing every unexpired held session with its salt-and-hmac, packed bytes, valid-until, protocol id, ALPN and max early data, with unit tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: a96ebcbf 3745c207 a549b4ea 3d836d5e.
