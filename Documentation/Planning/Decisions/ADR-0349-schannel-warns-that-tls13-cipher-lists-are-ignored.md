# ADR-0349 — The Schannel build warns that `--tls13-ciphers` and `--proxy-tls13-ciphers` are ignored

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1034.

## Context

ADR-0011 has the Schannel build ignore `--tls13-ciphers`, but curl 8.21.0's Schannel build
also says so and Curl did not. Measured with `Record-CurlExchange.ps1` (BL-1034 Notes):

- `Warning: ignoring --tls13-ciphers, not supported by libcurl with Schannel` and
  `Warning: ignoring --proxy-tls13-ciphers, not supported by libcurl with Schannel`, exit 0.
- Each is printed once per URL, before that URL's transfer starts (before `Trying`), for any
  scheme, plain `http://` included, and whether or not a proxy is used.
- The order is fixed whatever the command line's: the `--capath` warning (ADR-0009), then
  `--tls13-ciphers`, then `--proxy-tls13-ciphers`.
- `-s` silences them, `-sS` too. At 79 columns neither wraps (72 and 79 bytes).

The OpenSSL build honours both options and prints nothing.

## Decision

`CurlComposition.WarningLinesBeforeEachTransfer` builds the run's per-transfer warning lines:
the proxy TLS provider's `Warnings` (the `--capath` line), then, in the Schannel build, the
two lines above for whichever of the options is given. The runner already prints these lines
before each URL, wraps them at the terminal width and drops them under `-s`.

## Alternatives considered

- **Put the lines in `SslStreamTlsProvider.Warnings`.** Lost: a provider sees only its own
  `TlsClientOptions`, so the proxy provider cannot tell `--tls13-ciphers` from
  `--proxy-tls13-ciphers`, and only the proxy provider's warnings reach the runner.
- **Print them only for an `https://` URL or through a proxy.** Lost: measured curl prints
  them for a plain `http://` URL with no proxy.

## Consequences

The two warnings match curl's bytes on Windows; nothing changes elsewhere.
