# ADR-0797: The Schannel build takes a server certificate listed in `--cacert` as a trust anchor

- **Status:** Accepted
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3289
(found by BL-3235's CLI TLS differential case `tls-wrong-ca`).

## Context

`curl -sS --ssl-no-revoke --cacert server.pem https://host/`, where `server.pem` holds only the
server's own CA-issued certificate, exits 0 with curl 8.22.0's Schannel build (measured by
BL-3235's differential run against real curl: exit 0, body `ok`). Schannel curl builds an
exclusive-root chain engine from the file, and a certificate it lists ends the chain as an
anchor. .NET's `X509ChainTrustMode.CustomRootTrust` does not: the leaf's issuer is in neither
store, so the chain is `PartialChain` and Curl refused it with exit 60, "the certificate chain
is incomplete".

## Decision

`ServerCertificateVerification`, in the Schannel build with `--cacert`, drops
`RemoteCertificateChainErrors` when the chain's leaf is byte-equal to a certificate in the
chain policy's custom trust store and the chain's only faults are `PartialChain` and
`UntrustedRoot`. Any other fault (a date, a revocation status, a usage) still fails as before,
and the name check still runs. The OpenSSL build is unchanged here; curl's OpenSSL backend sets
`X509_V_FLAG_PARTIAL_CHAIN`, which trusts any listed certificate, and that wider rule is left to
its own task.

## Consequences

A self-signed or CA-issued server certificate pinned by listing it alone in `--cacert` works
on Windows as with curl. Pinned by `ServerCertificateVerificationTests.LeafAnchor.cs`.
