# ADR-1355: OCSP answers before the CRL below a non-self-signed anchor

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation (BL-5877, STD-196).

## Context

Below a non-self-signed `--cacert` or `--proxy-cacert` anchor the Windows chain engine
reports every revocation status unknown, so Curl reads revocation itself (BL-5384). It
read CRL distribution points only, so a certificate its OCSP responder had revoked was
accepted, and an OCSP-only good certificate was refused. curl 8.22.0 Schannel asks the
responder and prefers its answer over the CRL (measured by the fitness review of
2026-11-24 13:58).

## Decision

- Each certificate below the anchor is first asked about at the `http` OCSP responders its
  authority information access names, in order. The first answer of good or revoked
  decides, and no CRL is then fetched.
- With no such answer (no responder, fetch failed, status unknown, response unusable, or a
  good answer out of date) the CRL check decides as before; with no usable CRL either, the
  status is unknown.
- The request is RFC 6960's GET form (`{responder}/{escaped base64}`) with one SHA-1
  certificate identifier and no nonce. A request this small is what CryptoAPI sends by GET,
  and a GET reuses the one HTTP fetch the CRL check already has. POST is not sent.
- The response is checked by `Curl.Tls`'s `OcspStapleVerifier`, the check `--cert-status`
  uses: signed by the issuer or by a certificate it issued for `id-kp-OCSPSigning`,
  naming the certificate, `thisUpdate` and `nextUpdate` current within five minutes. A
  signed revoked answer counts whatever its dates, as an out-of-date delta list that
  lists the certificate does (BL-5857).
- OCSP fetches share the five-second budget and the connect limit of the list fetches.
