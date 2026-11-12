# ADR-1238 — Without a version option, alt-svc switches to h1 alone on Windows

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4912
- Decided by Claude under Stewart's delegation.

## Context

libcurl's `url_set_conn_peer` looks an alt-svc entry up with the destination ALPNs the build
allows: `h3` only `#ifdef USE_HTTP3`, `h2` only `#ifdef USE_HTTP2`, `h1` always. The reference
build on Windows, curl 8.22.0 Schannel, has neither HTTP/2 nor HTTP/3 (`-V` lists neither,
ADR-0141), so it allows `h1` alone. The outside fitness review of 2026-11-11 (findings STD-009
and STD-013, each reproduced at least twice) measured it: with no version option, an
`h1 localhost 18301 h3 localhost 18398` entry and an `h2 localhost 18320` entry both leave the
transfer on the origin (`ok`, `1.1 18301`, exit 0). Curl followed curl.se's build on every
platform, so the h3 entry failed with exit 56 when nothing answered on UDP and the h2 entry was
served over HTTP/2 on Windows.

## Decision

`AltSvcTransferCache.LookupOf` allows only `h1` as the destination when the transfer has no
version option and the system is Windows; elsewhere it still allows `h1`, `h2` and `h3`, as the
OpenSSL builds with nghttp2 and ngtcp2 do. A version option (`--http2`, `--http3`,
`--http3-only`) keeps the destinations it allowed before on every platform: Curl accepts those
options everywhere (ADR-0141, ADR-0144), and the reference build has no answer to match, since it
refuses them.

## Consequences

On Windows an `h2` or `h3` entry, from a file or learned from a header, never moves a transfer
without a version option off the origin; entries are still read, learned and written. The
Console tests that pin the switches are excluded on Windows, so Linux and macOS keep them, and a
Windows test pins each platform's answer.
