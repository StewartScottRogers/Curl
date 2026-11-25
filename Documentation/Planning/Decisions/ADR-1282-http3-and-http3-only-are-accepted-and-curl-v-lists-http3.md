# ADR-1282: --http3 and --http3-only are accepted and curl -V lists HTTP3

Decided by Claude under Stewart's delegation (BL-5289). Supersedes ADR-0540, ADR-0521 and ADR-0451.

## Context

ADR-0540 refused `--http3` and `--http3-only` with exit 2 because the 8.22.0 reference builds measured then had no QUIC library. Surl's Phase 7 measurement shows upstream curl 8.21.0 built with ngtcp2 and nghttp3 speaking HTTP/3 (`--http3-only` exits 0 and `%{http_version}` is `3`; with no QUIC listener it exits 56; `--http3` falls back over TCP). Upstream is the oracle, and the port's rule is that nothing is left out; its HTTP/3 transfer over hand-built QUIC already exists (BL-732).

## Decision

The `--http3` and `--http3-only` rows are plain flags on every platform; `CommandLineOption.RefusedWithoutQuic` and the AI-help refusal line are removed. `curl -V` lists `HTTP3` in `Features:` on every platform. `CommandLineOptions.ActsAsQuicBuild` stays, but only the alt-svc cache reads it.

## Consequences

Exit codes and fallback come from the existing HTTP/3 transfer, not from the parser.
