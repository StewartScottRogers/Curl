# ADR-0580 — `Libcurl`'s TLS option setters check values as curl 8.22.0's `setopt.c` does

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2403.

## Context

BL-2403 adds a setter row (ADR-0560) for every `CURLOPT_*` that `EasyTlsOptions` holds: the origin's
options, their `PROXY_*` twins, the origin-only options and `DOH_SSL_*`. Upstream answers some of
them by what the TLS backend supports, so the Schannel and OpenSSL builds differ, and Curl's TLS is
neither: it is its own managed engine.

## Decision

1. The rows live in `Libcurl/Options/Tls`. `TlsPeerOptionSetters` holds one instance method per
   twinned option, and two instances, `Origin` and `Proxy`, write `EasyTlsOptions.Origin` and
   `EasyTlsOptions.Proxy`, so each twin is one method rather than two. `EasyTlsOptionSetters` adds the
   rows and holds the origin-only and `DOH_SSL_*` setters.
2. Checked as `setopt.c` does: `SSLVERSION` refuses SSL 2, SSL 3, a minimum past TLS 1.3 and a maximum
   past `CURL_SSLVERSION_MAX_TLSv1_3` with `BadFunctionArgument`; `CA_CACHE_TIMEOUT` refuses below -1,
   takes -1 as forever and caps at `INT_MAX` seconds; `SSL_VERIFYHOST`, its proxy twin and
   `DOH_SSL_VERIFYHOST` are on when either low bit is set (`!!(arg & 3)`); `SSL_OPTIONS` keeps the low
   8 bits; `ECH` sets the mode, the `ecl:` list or the `pn:` name beside each other (ADR-0514) and
   ignores anything else.
3. Options libcurl gates on backend support (`CAPATH`, `PINNEDPUBLICKEY`, `SSL_VERIFYSTATUS`,
   `CERTINFO`, `TLS13_CIPHERS` and the like) are all accepted and stored: the managed engine is the
   backend, and whether a transfer honours one is the engine's to answer, not `setopt`'s. This keeps
   the answer the same on every platform.
4. Curl has no crypto engines, so `SSLENGINE` given a name and `SSLENGINE_DEFAULT` answer `NotBuiltIn`,
   as libcurl's engine-less backend (`Curl_none_set_engine`, the Schannel build's) does; `SSLENGINE`
   given `null` or empty clears it. `SSL_FALSESTART` answers `NotBuiltIn`, as curl 8.22.0 does with no
   backend left that has False Start.
5. Blobs are copied, as `CURL_BLOB_COPY` does, so a caller changing its array later changes nothing.
   `SSL_CTX_FUNCTION` and `SSL_CTX_DATA` are the callback group's rows.

## Consequences

`EngineIsDefault` and `FalseStart` stay at their defaults through `SetOption`. Strings and blobs are
not checked against `CURL_MAX_INPUT_LENGTH`, as no other group checks it yet.
