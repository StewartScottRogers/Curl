# ADR-1129 — The Certificate Transparency extension requires an embedded SCT from a log in a v3 log list

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4152.

## Context

BL-4152 adds an extension (ADR-0999) that requires valid Signed Certificate Timestamps (SCTs) on the server's
certificate, following RFC 6962 and RFC 9162. Curl's certificate check hook (ADR-1016) hands an extension the DER
certificates the server sent after curl's own verification accepted them, and lets it add a refusal as exit 60. A
client cannot check an SCT without the public keys of the logs it trusts, and curl has no such list.

## Decision

- **Option:** `--require-sct <log list>`, from `Curl.Extension.CertificateTransparency.UnitLibrary`; the extension is
  listed as `certificate-transparency`. Its value is a log list file in the version 3 JSON format Chrome and Apple
  publish (`https://www.gstatic.com/ct/log_list/v3/log_list.json`): every `operators[].logs[].key` is a log's base64
  DER `SubjectPublicKeyInfo`. Each key is filed under its log ID, the SHA-256 of that key (RFC 6962 §3.2), worked out
  here rather than taken from the list's `log_id`. The file is read for each new TLS connection; when it cannot be
  read or parsed the connection is refused (exit 60) with a message naming the file, so a bad list never lets a
  chain through.
- **Specification followed:** version 1 SCTs, RFC 6962 §3.2 and §3.3, checked as RFC 6962 §5.2 and RFC 9162 §8.1.2 and
  §8.1.3 describe: the SCT list is read from the certificate's X.509v3 extension `1.3.6.1.4.1.11129.2.4.2`; the
  `TBSCertificate` is rebuilt without that extension, the issuer key hash is the SHA-256 of the next certificate's
  `SubjectPublicKeyInfo`, and the log's signature (SHA-256 with ECDSA or RSA PKCS #1 v1.5) must verify over the
  precertificate entry. An SCT is valid when its log is in the list, its timestamp is no later than now and its
  signature verifies. **At least one** valid SCT lets the chain stand. SCTs of another version are skipped, since
  their layout is unknown; a malformed list refuses the chain.
- Without `--require-sct` no hook is activated and every transfer is unchanged; with `CURL_EXTENSIONS=0` the option
  is not registered, so curl refuses it as an unknown option (exit 2).

## Left out, and why

- **SCTs in the TLS `signed_certificate_timestamp` extension and in a stapled OCSP response** (RFC 6962 §3.3): the
  certificate check hook gives an extension only the certificates, not the handshake. Publicly trusted certificates
  embed their SCTs, so this covers nearly every server; a hook carrying the handshake's SCTs would be its own task.
- **RFC 9162 version 2 SCTs (`TransItem`) and inclusion proofs** (RFC 9162 §8.1.4): no version 2 log operates, and
  fetching proofs means contacting the log during the transfer, which curl never does.
- **Browser CT policies** (how many SCTs, from how many operators, by certificate lifetime) and the log list's
  `state` (usable, retired, rejected) and temporal intervals: they are vendor policy, not the RFCs. A user who wants
  only usable logs gives a list holding only those.

## Consequences

- The check runs after curl's own verification (ADR-1016), so it can only add a refusal: a chain curl refused never
  reaches it.
- Tests build certificates with `CertificateRequest`, sign SCTs written out byte by byte from RFC 6962, and drive the
  hook with no network.
