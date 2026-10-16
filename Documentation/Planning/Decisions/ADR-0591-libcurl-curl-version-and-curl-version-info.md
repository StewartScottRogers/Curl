# ADR-0591 — `Libcurl`'s `curl_version` and `curl_version_info` read the engine's `VersionInfo`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2458.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` lists `curl_version` and
`curl_version_info` (`CURLVERSION_*`, feature bits matching `-V`). The seam's `CurlVersionInfo`
(ADR-0509) carries the version, version number, host, feature names, protocols and TLS backend, and
leaves the `CURL_VERSION_*` bitmask to `Libcurl`. It carries no versions of bundled libraries
(zlib, brotli, nghttp2 and the rest): Curl builds those by hand or takes them from the BCL, and its
`curl -V` first line names only the TLS backend after `libcurl/8.22.0`.

Upstream's `curl_version_info(stamp)` ignores `stamp` and always answers its one static struct, aged
`CURLVERSION_NOW`; an older caller simply reads fewer fields. `curl.h` 8.22.0 has twelve ages
(`CURLVERSION_FIRST` 0 to `CURLVERSION_TWELFTH` 11, `NOW` = `TWELFTH`) and the bits `1<<0` to `1<<30`.

## Decision

1. `Libcurl/Misc/Version` holds `CurlVersion`, `CurlVersionAge`, `CurlVersionFeatures` and
   `CurlVersionInfoData`, each a library-wide call over a `CurlLibrary`'s engine, since
   `curl_version` has no handle: `CurlVersion.Text(library)` and `CurlVersion.Info(library, age)`.
   Both answer after `curl_global_cleanup`, as libcurl's do without any initialisation; a `null`
   library throws `ArgumentNullException`.
2. `Text` is `libcurl/<Version>` and, when the engine has a TLS backend, a space and its name -
   exactly the tail of `curl -V`'s first line - and nothing more, since the seam names no other
   library.
3. `Info` ignores the age, as libcurl does, and answers every field of `CURLVERSION_NOW` with
   `Age` = `Twelfth`. `features` is the OR of `lib/version.c`'s bit for each feature name, matched
   exactly (`CurlVersion.FeatureBit`); a feature with no bit (`ECH`, `HTTPSRR`, `SSLS-EXPORT`) or a
   name libcurl does not list adds none. `ssl_version_num` is 0 as upstream's always is; every field
   naming a library the seam does not report (`libz_version`, `ares`, `libidn`, `libssh_version`,
   brotli, nghttp2, QUIC, `cainfo`, `capath`, zstd, hyper, gsasl, `rtmp_version`) is `null` or 0,
   what libcurl answers for a library it was built without.
4. Agreement with `curl -V` over the real `ManagedCurlEngine` - host, TLS backend, the protocol list
   without the tool's own `ipfs` and `ipns`, and the feature names - is the engine's to report.
   `Curl.Engine.UnitLibrary` was held by another lane, so that is BL-2464.

## Consequences

A caller that checks `libz_version` or `brotli_version` for a library's presence sees none even
though the `libz` and `brotli` feature bits are set; one that checks the feature bits, as curl's own
tool does, sees the truth. If the seam later carries library versions, `Info` fills them in.
