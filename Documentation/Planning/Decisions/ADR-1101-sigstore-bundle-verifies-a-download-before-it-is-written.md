# ADR-1101 — `--sigstore-bundle` verifies a download against a Sigstore bundle before it is written

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4136.

## Context

BL-4136 adds the extension `Curl.Extension.Sigstore.UnitLibrary` (ADR-0999): check a downloaded file against a
Sigstore bundle and an expected signer identity, as `cosign verify-blob --bundle` does. Upstream curl has no such
option. The specifications followed are the Sigstore bundle format (protobuf-specs `sigstore_bundle.proto`, media
types `application/vnd.dev.sigstore.bundle.v0.1` to `v0.3`, in proto3 JSON), the Sigstore client specification's
"Verification" section, and Fulcio's certificate specification ("Certificate Extensions"). A transfer hook can run
curl's handler with the body captured and write it afterwards (ADR-1019).

## Decision

- **Names.** `--sigstore-bundle <file>` turns it on. `--sigstore-identity <identity>` (the e-mail address or URI the
  signing certificate's subject alternative name must hold, compared exactly) and `--sigstore-root <pem-file>` (the
  Fulcio CA certificates to trust) are needed with it; `--sigstore-issuer <url>` also demands the OIDC issuer
  extension (`1.3.6.1.4.1.57264.1.8`, or the deprecated `1.3.6.1.4.1.57264.1.1`). The extension is `sigstore` in
  `curl --extensions` and `curl --ai-help extensions`. No scheme: it applies to every transfer of the option group.
- **Order of checks**, as cosign: the bundle's `messageDigest` must be `SHA2_256` of the received body; the
  certificate must chain to a self-signed certificate in the root file (the file's other certificates serve as
  intermediates) at the first transparency log entry's `integratedTime`, without revocation checks, for the
  code-signing extended key usage; the `messageSignature.signature` must verify as ECDSA P-256 SHA-256 (DER) over the
  body with the certificate's key; then identity and issuer.
- **Outcome.** The body is held back while the transfer runs (headers pass as they arrive) and written only when every
  check passes. A failed check writes no body and fails with exit code 60 (`CURLE_PEER_FAILED_VERIFICATION`), the
  exit code curl gives when a peer's certificate is not trusted, the nearest meaning curl has. A missing identity or
  root fails with exit 2 before any transfer; an unreadable bundle or root file with exit 26. A transfer that fails
  itself keeps its own result.
- **No embedded trust root.** The root is supplied by the user rather than embedded: an embedded Sigstore public-good
  root would go stale with its rotation, and no TUF client exists here to refresh it.

## Left out, and why

- **Transparency log proofs.** The Rekor signed entry timestamp, inclusion proof and checkpoint signature are not
  verified; `integratedTime` is trusted as given. That needs Rekor's public key and the trusted root's
  `tlogs` from `trusted_root.json`, which a later task can add with a `--sigstore-trusted-root` option.
- **DSSE envelopes** (`dsseEnvelope` bundles, in-toto attestations): only `messageSignature` bundles sign a blob.
  A bundle holding a DSSE envelope is refused as unreadable (exit 26).
- **Public-key bundles** (`publicKey` hint, no certificate) and RSA or Ed25519 signing keys: Fulcio issues ECDSA
  P-256 certificates, which are what keyless signing gives.
- **Regular-expression identities** (`--certificate-identity-regexp`): an exact match is the safe default.
- **RFC 3161 timestamps** in `timestampVerificationData`.
