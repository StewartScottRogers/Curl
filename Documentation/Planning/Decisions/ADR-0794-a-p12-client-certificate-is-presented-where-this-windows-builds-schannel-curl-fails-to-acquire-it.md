# ADR-0794: A P12 client certificate is presented where this Windows build's Schannel curl fails to acquire it

- **Status:** Accepted
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3290
(found by BL-3235's CLI TLS differential run, case `tls-mtls-p12`).

## Context

With `--cert client.p12:pw --cert-type P12` against a server that requires a client certificate,
Curl presents the certificate and exits 0 with the body. The reference curl exits 35 with
`curl: (35) schannel: AcquireCredentialsHandle failed: SEC_E_UNKNOWN_CREDENTIALS (0x8009030d) - The credentials supplied to the package were not recognized`.

Measured 2026-10-23 on the lane machine (Windows 11 26H2, build 26300, UBR 9711) against a loopback
`SslStream` server requiring a client certificate, with `--cacert ca.pem` from the tester's `pki`:

| Client P12 | curl 8.22.0 (ucrt64, Schannel) | curl 8.21.0 (`C:\Windows\System32\curl.exe`) |
|---|---|---|
| tester's `client.p12` (ECDSA P-256, OpenSSL 3, AES-256 PBES2), password `pw` | 35, text above | 35, text above |
| CA-issued RSA-2048 and ECDSA P-256, 3DES-SHA1 and AES-256 PBES2, password `pw` | 35 | 35 (RSA) |
| the same, empty password | 35 | not measured |
| self-signed `CN=localhost` RSA-2048, empty password (ADR-0009's case) | 35 | not measured |
| any of them with `-k` instead of `--cacert`, or without `--cert-type` | 35 | not measured |
| tester's `client.p12` with the wrong password | 58 `Failed to import cert file …, password is bad` | not measured |

So both builds now fail for every P12 file, whatever its key type, encryption or password. The
failure happens after the TCP connect (a closed port still gives 7) and before the server sees a
ClientHello (the server logs an EOF). Bad passwords, missing files and PEM files still give 58,
as ADR-0009 records. ADR-0009 measured exit 0 for the self-signed RSA P12 with curl 8.21.0
Schannel on 2026-09-26. That certificate now fails with the same curl. What changed is the
operating system: Schannel turns down the credential curl builds from the imported PFX.

## Decision

Curl keeps presenting a P12 client certificate on Windows and does not reproduce exit 35. The
failure is Schannel refusing the credential on this Windows build. It is not how curl itself
behaves: the same curl succeeded a month earlier, and it presumably still succeeds on Windows
builds that accept the credential. To copy it, Curl would have to guess which Windows builds
refuse. And on those builds no file-based client certificate could work at all, since curl
loads PEM `--cert` files on neither build. The failures that come from curl itself still match:
a bad password, a missing file, a PEM file, a wrong `--cert-type` (ADR-0009).

## Consequences

- `tls-mtls-p12` stays a difference in the differential runner on this machine. This ADR
  explains that difference; it is not a defect in Curl. Tagging the case so the runner skips it
  is filed as a separate task, because the case file and the limitation list belong to
  `Curl.Conformance.Differential.UnitLibrary`.
- If a later Windows or curl release brings back exit 0, the runner shows no difference and
  this ADR has nothing left to explain.
