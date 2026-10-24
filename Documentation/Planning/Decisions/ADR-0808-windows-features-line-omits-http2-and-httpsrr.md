# ADR-0808: The Windows curl -V Features line lists neither HTTP2 nor HTTPSRR

- Status: Accepted
- Date: 2026-10-23
- Decision: Decided by Claude under Stewart's delegation (BL-3359), after Stewart accepted audit finding AF-0382
- Supersedes: the Features-line part of [ADR-0786](ADR-0786-windows-build-runs-http2.md); the Windows part of
  the `HTTPSRR` entry in [ADR-0021](ADR-0021-v-version-keeps-curls-format-and-lists-only-what-curl-implements.md)'s amendment

## Context

ADR-0786 turned Curl's own HTTP/2 on for Windows and, with it, put `HTTP2` on the Windows `curl -V`
Features line. ADR-0021's amendment lists `HTTPSRR` on every platform because the DoH resolver reads HTTPS
records. The Schannel reference build (curl 8.22.0, x86_64-w64-mingw32) lists neither:

```
Features: alt-svc AsynchDNS brotli Debug HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd
```

Audit finding AF-0382 reported the difference: a script that greps `-V` for `HTTP2` takes a different
path under Curl than under the curl it replaces. Stewart accepted the finding.

## Decision

- `CurlVersionText.WindowsFeaturesLine` drops `HTTP2` and `HTTPSRR`, and so matches the Schannel
  reference build's line word for word apart from its `Debug` (the reference is a debug build).
- `--http2`, `--http2-prior-knowledge` and `--proxy-http2` still run on Windows (ADR-0786's option part
  holds), and the DoH resolver still reads HTTPS records: only what `-V` prints changes.
- The Linux and macOS line (`FeaturesLine`) is unchanged.
- `curl_version_info` in Libcurl (`ManagedCurlVersion`) is outside this decision's task; it keeps its own list.

## Consequences

- `curl -V` on Windows is byte-for-byte the reference's Features line again (less `Debug`).
- A Windows script that checks `-V` for `HTTP2` before passing `--http2` now skips HTTP/2, as it does
  with the curl it replaces, though Curl would have run it.
