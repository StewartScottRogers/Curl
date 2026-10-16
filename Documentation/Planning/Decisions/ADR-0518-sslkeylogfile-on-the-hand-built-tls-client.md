# ADR-0518 — `SSLKEYLOGFILE` runs on the hand-built TLS client, off Windows only

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2327.

## Context

Gap finding GF-0084: upstream test2090 sets `SSLKEYLOGFILE` and runs `--tls-max 1.2` against an
HTTPS server, then expects one `CLIENT_RANDOM <64 hex> <96 hex>` line in the file. curl 8.22.0's
OpenSSL build reads the variable once and hands OpenSSL a key-log callback that serves every
connection of the process; a file it cannot `fopen` is skipped silently. The Schannel build has no
such support (test2090 requires `!Schannel`). `SslStream` cannot hand out its secrets on any
platform without a process-wide switch that is off in release runtimes.

## Decision

1. `Curl.Engine`'s `TlsClientOptionsMapping` reads `SSLKEYLOGFILE` into
   `TlsClientOptions.KeyLogFile` for the target's, an HTTPS proxy's and the DoH server's handshakes,
   off Windows only; an empty value is unset. On Windows Curl matches the Schannel build and ignores
   the variable.
2. `TlsClientRouting` gains a row: with `KeyLogFile` set the connection runs on `HandBuiltTlsProvider`,
   the only client that can see its secrets.
3. `Curl.Tls` gets `ITlsKeyLog`; `Tls12ClientSettings.KeyLog` receives the `CLIENT_RANDOM` entry once
   the keys are installed, full handshake or resumed. `Curl.Tls` stays pure: the file writer,
   `NssKeyLogFile`, lives in `Curl.Networking` and appends lowercase hex lines, skipping a file it
   cannot write as curl does.
4. TLS 1.3's five secrets are BL-2339's work; test2090 caps the version at TLS 1.2.

## Consequences

Setting `SSLKEYLOGFILE` off Windows moves every TLS connection of the run to the hand-built client,
whose ClientHello already matches the OpenSSL build's (ADR-0235). QUIC is unchanged.
