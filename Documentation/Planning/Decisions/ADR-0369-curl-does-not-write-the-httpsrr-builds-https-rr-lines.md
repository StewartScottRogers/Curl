# ADR-0369 — Curl does not write the `--enable-httpsrr` build's `HTTPS-RR:` lines

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1173, following ADR-0359. curl 8.21.0 on OpenSSL 4.0.0, built with `--enable-ech
--enable-httpsrr`, was measured through `--doh-url` against `Record-CurlExchange.ps1 -DohPort`,
which answers HTTPS (type 65) queries with one record, `1 . ech=<list>`. Besides the `ECH:` lines
ADR-0359 took from source, which matched it line for line, curl wrote:

- `Some HTTPS RR to process` before `Host ... was resolved.` when an HTTPS record came back;
- `HTTPS-RR: 1 . ech=<64 bytes>` after the address lines, or `HTTPS-RR: -` with no record;
- an HTTPS query on every DoH resolve, `--ech` or not, `grease` and `false` included.

All three come from `--enable-httpsrr`, a build feature of its own, not from ECH.

## Decision

Curl writes none of these lines, and asks the HTTPS query only under `--ech true` or `hard`
(BL-707, ADR-0312). The builds Curl matches - Schannel on Windows, the distributions' OpenSSL
builds on Linux and macOS - are not built with `--enable-httpsrr`, and ADR-0359 already left out
`HTTPS-RR: -` on the same grounds. The `ECH:` lines stay, as `--ech` is implemented.

## Consequences

A transcript of Curl under `--ech true --doh-url` lacks the two `HTTPS-RR` lines an
ECH-and-HTTPSRR curl writes. Should the platform builds enable HTTPS RR, this is reopened as a
task that adds the lines and the unconditional query together.

## Alternatives considered

- Writing the lines whenever Curl asks the HTTPS query: it would match no build, since the
  HTTPSRR build asks on every resolve and writes `HTTPS-RR: -` even without `--ech`.
