# ADR-0961: A DoH URL with no scheme is https

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3567 (CGR-038)
- Decided by Claude under Stewart's delegation

## Context

`EngineComposition.DohUrlOf` put `http://` in front of a `--doh-url` value with no scheme, after
a BL-642 note that such a URL reached its server as plain HTTP. The third-party gap report's
CGR-038 found curl 8.22.0 (Schannel and OpenSSL) treating it as `https`. Measured again on curl
8.22.0 Schannel: a loopback listener given as `--doh-url 127.0.0.1:21840/dns-query` received a TLS
ClientHello (`16 03 01 ...`), and curl ended with `curl: (28) Resolving timed out after 3031
milliseconds` under `-m 3`.

## Decision

A `--doh-url` value without `://` gets `https://` in front. A value with a scheme is used as it
is; anything but `http` or `https` stays unusable (exit 6, as before).

## Consequences

A scheme-less DoH URL is tunnelled through TLS on port 443 unless the value names a port, as in
curl; `--doh-url bogus` now asks `bogus:443` over TLS before failing with exit 6.

It amends ADR-0859 (CGR-163), whose release-build rule counted a scheme-less value as `http`: such a
value is `https`, so it is used and dialled over TLS; only an explicit `http://` DoH URL fails every name
with exit 6 without reaching the server.
