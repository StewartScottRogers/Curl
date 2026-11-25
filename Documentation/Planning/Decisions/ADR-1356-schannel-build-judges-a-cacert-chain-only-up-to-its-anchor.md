# ADR-1356: The Schannel build judges a --cacert chain only up to its anchor, by Windows' trust error

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation (BL-5885, STD-204).

## Context

With a `--cacert` file holding a non-self-signed anchor, .NET's chain carries `PartialChain`
beside every other fault, and its elements above the anchor are the server's. The Schannel
build tolerated such a chain only when nothing else was wrong anywhere in it, and otherwise
said `schannel: the certificate chain is incomplete`. curl on Schannel builds the chain with
the file's certificates as its only anchors, names any fault by its `CERT_TRUST_*` value,
accepts an anchor with no basic constraints and ignores what is above the anchor.

Measured with curl 8.23.0 (Windows) and 8.21.0 (Git for Windows), both Schannel, against
`openssl s_server`, `--ssl-no-revoke`: an anchor without keyCertSign or a client-only leaf
`schannel: error 0x00000010`; an anchor with `CA:FALSE` or a broken path length `0x00000400`;
`requireExplicitPolicy` `0x02000000`; an unknown critical extension `0x08000100`; an anchor
with no basic constraints, and a path length broken above the anchor, exit 0. With revocation
checking on, every one of these certificates, which name no CRL, is
`schannel: the revocation status is unknown`.

## Decision

In the Schannel build, for a chain that finds no trusted root and holds a certificate the
`--cacert` file lists:

- A fault that only elements above the anchor carry does not count, nor does
  `InvalidBasicConstraints` on an anchor with no basic constraints extension. A fault no
  element carries (`NoIssuanceChainPolicy`) counts.
- Any remaining fault other than a missing root or an unknown revocation status is exit 60,
  named as a `--cacert` chain's faults already are (`schannel: error 0x<mask>`, and the
  time, revoked and revocation-unknown messages before it). The CRLs below the anchor
  (ADR-1294) say whether revocation is clean, revoked or unknown.

The OpenSSL build is unchanged: it reads the whole chain, as before.

## Consequences

.NET's `X509ChainStatusFlags` share the `CERT_TRUST_*` values, so the mask needs no table.
A policy fault raised by a certificate above the anchor cannot be told from one below it,
for .NET puts it on no element, and is reported.
