# ADR-0798: The OpenSSL build takes any certificate of the chain listed in `--cacert` as a trust anchor

- **Status:** Accepted
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3335
(follow-up to ADR-0797).

## Context

curl's OpenSSL backend sets `X509_V_FLAG_PARTIAL_CHAIN` by default. Measured with curl 8.18.0 on
OpenSSL 3.5.5 (WSL Ubuntu, `openssl s_server` with a root, an intermediate and a leaf):
`curl --cacert <file> https://localhost/` exits 0 when the file holds only the leaf, only the
intermediate, or only the root.

## Decision

`ServerCertificateVerification` accepts, on the OpenSSL build with `--cacert`, a chain whose only
faults are a missing root when any certificate of the chain is listed in the file. The Schannel
build keeps ADR-0797's rule: the leaf only. `--ssl-no-partialchain` is not an option this tree
parses (curl 8.18.0 rejects it too), so the rule has no off switch.

## Consequences

The OpenSSL-build test that pinned a leaf-only file as exit 60 now pins exit 0.
