# ADR-0367 — A completed `--ech` handshake writes curl's `ECH: result:` line

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1170, following ADR-0359. curl 8.21.0 on OpenSSL 4.0.0's ECH build writes
`ECH: result: status is ..., inner is ..., outer is ...` straight after `SSL connection using`
for a completed handshake under `--ech`. ADR-0359 measured three texts (`grease`, `true` with
no usable list, an accepted offer under `-k`); the verified success text is only in
`lib/vtls/openssl.c`.

## Decision

1. `TlsHandshakeEvent` carries the text after `ECH: result: ` as `EchResult`; the hand-built
   provider fills it in (`EchResultText`) and `Curl.Output`'s OpenSSL handshake lines write it
   after `SSL connection using`. It is null for `--ech false` or no `--ech`.
2. `grease` writes `sent GREASE, inner is NULL, outer is NULL`, at TLS 1.2 as at TLS 1.3;
   `true` with nothing offered writes `not configured, inner is NULL, outer is NULL`.
3. An offer that completed was accepted: the hand-built client aborts a rejected offer with
   `ech_required` (exit 101), so no completed handshake is a rejection. Under `-k` the text is
   `bad name (tolerated without peer verification)`, as measured; verified, `success`, from
   source. The outer name is the offered configuration's public name, which is `pn:`'s when
   given.
4. The Schannel build writes nothing: only the OpenSSL handshake lines read the field, matching
   the platform builds, which have no ECH (ADR-0359 point 4).

## Consequences

The verified `success` text is unmeasured until a test server that accepts ECH and presents a
certificate for the inner host is available; the text follows curl's source.

## Alternatives considered

- Plumbing the TLS client's own accept flag through the handshake result: redundant, since a
  rejection never completes, and it would widen the hand-built handshake's contract for no
  different output.
