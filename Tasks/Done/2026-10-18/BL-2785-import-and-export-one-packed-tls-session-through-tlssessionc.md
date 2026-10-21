---
id: BL-2785
title: Import and export one packed TLS session through TlsSessionCache, as curl_easy_ssls_import and _export need
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HeldTlsSession.cs, Curl.Networking.UnitLibrary/TlsSessionCache.cs, Curl.Networking.UnitLibrary/TlsSessionPacking.cs, Curl.Networking.UnitTests/TlsSessionCacheTests.cs, Curl.Networking.UnitTests/TlsSessionPackingTests.cs]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2785 — Import and export one packed TLS session through TlsSessionCache, as curl_easy_ssls_import and _export need

## Goal

`TlsSessionCache` imports one packed session (curl 8.22.0's `vtls_spack.c` format, `TlsSessionPacking`) under a peer key or a salt-and-hmac, and lists every held session for export, so the engine can implement `curl_easy_ssls_import` and `curl_easy_ssls_export`.

## Context

Found by BL-2669. Today the cache only reads and writes a whole `--ssl-sessions` file (`Import(text, fileName)`, `Export()`). libcurl's `curl_easy_ssls_import(easy, session_key, shmac, shmac_len, sdata, sdata_len)` (curl 8.22.0 `lib/vtls/vtls_scache.c` `Curl_ssl_session_import`) adds one packed session: under `session_key` when given, else under the 64-byte salt-and-hmac (`CURLE_BAD_FUNCTION_ARGUMENT` when neither, or a hmac not 64 bytes), answering `CURLE_BAD_FUNCTION_ARGUMENT` for bytes `Curl_ssl_session_unpack` refuses. `curl_easy_ssls_export` calls back once per held session with the key's salt and hmac, the packed bytes, `valid_until`, the IETF TLS id, ALPN and max early data. lib1678 imports, 64 times under `example.test:443`, a packet holding a 4096-byte ticket, a 127-byte QUIC transport parameter block, a 10-byte ALPN, then a second one-byte ticket, ALPN and QUIC block (later tags replace earlier ones in upstream's unpack); every import must answer OK. `ImportLine` already maps unpack and hmac failures to curl's codes 26 and 43; reuse that logic.

## Acceptance criteria

- [x] `TlsSessionCache` has a public method importing one packed session under a peer key or a 64-byte salt-and-hmac, answering curl's code for each refusal (bad packing, neither key nor 64-byte hmac), with unit tests in `Curl.Networking.UnitTests/TlsSessionCacheTests.cs`.
- [x] `TlsSessionPacking.Unpack` accepts lib1678's packet (repeated ticket, ALPN and QUIC parameter tags, the later value kept) with a unit test pinning it.
- [x] `TlsSessionCache` has a public method listing every unexpired held session with its salt-and-hmac, packed bytes, valid-until, protocol id, ALPN and max early data, with unit tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added Curl.Networking.UnitLibrary/HeldTlsSession.cs to touches: the export listing needs a public record and the style rules want one public type per file. No task in Doing on origin/work/dark-factory names it.
- `TlsSessionCache.ImportSession(peerKey, saltAndHmac, packed)` returns curl's code: 26 (CURLE_READ_ERROR) for bytes Curl_ssl_session_unpack refuses, as upstream's import and `ImportLine` answer, and 43 for neither a key nor a 64-byte salt and hmac. The task's Context said 43 for bad packing, but upstream returns the unpack result (26) and ImportLine already pins 26, so 26 it is. `ImportLine` now calls it.
- `TlsSessionCache.ListSessions()` returns `HeldTlsSession` (peer key or null, salt and hmac, packed bytes, valid-until, IETF id, ALPN, max early data); `Export()` is built on it.
- `TlsSessionPacking.Unpack` already kept the later value of a repeated tag; `Unpack_Lib1678RepeatedTags_KeepsTheLaterValues` pins it. A session without a valid-until (lib1678's) is held with 0 and so drops out at the next expiry sweep; upstream's default lifetime for such a session is the engine task's (BL-2669) to add if lib1678 needs it.
- Measured: Measure-CodeQuality -Library Curl.Networking.UnitLibrary, 0 failing members; fast tests 41 projects, 0 failed.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: a96ebcbf 3745c207 a549b4ea 3d836d5e.
- 2026-10-18: Doing -> Done. TlsSessionCache imports one packed session under a peer key or salt-and-hmac (codes 26/43) and lists every held session for curl_easy_ssls_export
