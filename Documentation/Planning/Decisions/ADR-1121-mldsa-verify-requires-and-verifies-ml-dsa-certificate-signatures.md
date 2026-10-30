# ADR-1121 — --mldsa-verify requires and verifies ML-DSA certificate signatures

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4153.

## Context

BL-4153 adds ML-DSA signed certificates as an extension (ADR-0999) in `Curl.Extension.MlDsaCertificates.UnitLibrary`:
an option that accepts and verifies ML-DSA certificate signatures. curl has no such option, so the extension is
tested against FIPS 204 (13 August 2024) and RFC 9881 (Internet X.509 Public Key Infrastructure: Algorithm Identifiers
for ML-DSA), the versions followed, and never changes what curl does without the option. ADR-1016's certificate
check hook runs an extension's check on every chain curl's own verification accepted and can only refuse one.

## Decision

- **Option.** One flag, `--mldsa-verify`, with no argument; curl has no option of that name. It is a certificate check
  hook (ADR-1016), registered in the option, description and certificate check hook catalogues; it has no URL
  scheme, so, as ADR-1106's `--ratelimit-pace`, it adds no line to `ExtensionCatalogue`.
- **What it requires.** The server's own certificate must be signed with `id-ml-dsa-44`, `id-ml-dsa-65` or
  `id-ml-dsa-87` (RFC 9881 section 2); otherwise the connect fails with exit 60. A flag that let a classically signed
  chain through would verify nothing the user can see.
- **What it verifies.** Every certificate in the chain signed with ML-DSA: its algorithm identifier has no parameters
  (section 2) and equals the `tbsCertificate`'s `signature` field (RFC 5280 section 4.1.1.2); its issuer is the next
  certificate the server sent, or itself when it is last and self-issued, and that issuer's subject is its issuer
  name; the issuer's `subjectPublicKeyInfo` is the same ML-DSA parameter set with no parameters and a public key of
  that set's length (section 4); and the signature verifies under FIPS 204 algorithm 3 (`ML-DSA.Verify`, pure ML-DSA)
  over the DER `tbsCertificate` with the empty context string (section 3). Any failure is exit 60 naming the
  certificate's position and the section it breaks.
- **Links signed otherwise** (an RSA or ECDSA intermediate above an ML-DSA leaf) are left to curl's own verification,
  which already accepted them.
- **Cryptography.** ML-DSA verification is `Curl.Cryptography.MlDsa.VerifyData`, the hand-built FIPS 204
  implementation already in the shared layer; the certificate is read with `System.Formats.Asn1`.

## Left out, and why

- **Anchors in the trust store.** An ML-DSA signature whose issuer the server did not send is refused, not looked up
  in `--cacert` or `--capath`: the hook sees only the chain the server sent (ADR-1016), and curl's own verification of
  the anchor already ran. A server that wants `--mldsa-verify` sends its intermediates, as RFC 8446 section 4.4.2 asks.
- **HashML-DSA and composite signatures.** RFC 9881 uses pure ML-DSA only; pre-hash and composite (draft-ietf-lamps-
  pq-composite-sigs) identifiers are not ML-DSA signatures under it, so they count as signed otherwise.
- **CRLs, OCSP responses and private keys** (RFC 9881 sections 3 and 6) are not part of a server's chain.
- **QUIC.** A QUIC connection's chain is not reported to certificate check hooks (ADR-1016), so HTTP/3 connects pass
  unchecked.
