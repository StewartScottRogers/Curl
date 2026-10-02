# ADR-0330 — `--no-sessionid` and `--ssl-allow-beast` run on the hand-built client, which resumes by default

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-713.

## Context

ADR-0151 gives `--no-sessionid` and `--ssl-allow-beast` to the hand-built TLS client, since
`SslStream` can neither stop the operating system's process-wide session cache nor turn off
the TLS 1.0 CBC split. Two questions were left: whether the hand-built client resumes at all
without `--ssl-sessions` (until now it offered and kept sessions only with that option), and
which connections `--ssl-allow-beast` routes.

Measured 2026-10-01 with curl 8.18.0, OpenSSL 3.5.5 (Ubuntu under WSL), against
`openssl s_server -www` on loopback, two URLs on one command line with `-H "Connection: close"`:

- Default: the second handshake resumes. `-v` prints `* SSL reusing session with ALPN '-'`
  before its ClientHello, the server sends no Certificate or CertificateVerify, and the
  server's statistics count one session cache hit.
- `--no-sessionid`: both handshakes are full (Certificate and CertificateVerify each time),
  and no reuse line is printed.
- `--tlsv1.0 --tls-max 1.0 --ciphers AES128-SHA:@SECLEVEL=0` against a TLS 1.0
  `AES128-SHA` server (`s_server -tls1 -msg`): the client's first application data records
  are `17 03 01 00 24` (an empty record: 20-byte MAC plus 16 bytes of padding) then
  `17 03 01 00 64` (the request). With `--ssl-allow-beast` only `17 03 01 00 64` is sent.
  This is OpenSSL's empty fragment, as ADR-0150 records, not Schannel's 1/n-1 split.

## Decision

1. **The hand-built client resumes by default.** `CurlComposition.CreateTlsProvider` gives
   every hand-built provider the run's `TlsSessionCache`, whether or not `--ssl-sessions`
   names a file: a later connection to the same peer offers the TLS 1.3 session an earlier one
   received, as curl does. `--ssl-sessions` still alone loads and saves the cache.
2. **`--no-sessionid` routes every connection to the hand-built client**
   (`TlsClientOptions.NoSessionId`), which then neither takes a session from the cache nor
   keeps the tickets it receives. It reaches the HTTPS proxy's handshake too, as libcurl's
   `CURLOPT_SSL_SESSIONID_CACHE` applies to both.
3. **`--ssl-allow-beast` routes only a range that reaches TLS 1.0**
   (`TlsClientOptions.AllowBeast` with a `--tlsv1.0` minimum; a TLS 1.0 or 1.1 ceiling is
   hand-built already). Elsewhere no TLS 1.0 record is ever written, so the option has nothing
   to change and `SslStream` keeps the transfer's bytes. On the hand-built client it sets
   `Tls12ClientSettings.InsertEmptyFragment` off. `--proxy-ssl-allow-beast` does the same for
   the proxy's handshake.

## Consequences

- A transfer that runs on the hand-built client for another reason (`--curves`, `--cert-status`
  and so on) now resumes its second connection, as the OpenSSL build does.
- curl's `* SSL reusing session with ALPN '-'` `-v` line is not written yet.

## Alternatives considered

- **Resume only under `--ssl-sessions`.** Then `--no-sessionid` would change nothing on the
  hand-built path, and transfers routed there for other options would never resume, unlike
  curl. Rejected.
- **Route every `--ssl-allow-beast` transfer to the hand-built client.** It would change the
  ClientHello of every TLS 1.2 and 1.3 transfer for an option that only affects TLS 1.0.
  Rejected.
