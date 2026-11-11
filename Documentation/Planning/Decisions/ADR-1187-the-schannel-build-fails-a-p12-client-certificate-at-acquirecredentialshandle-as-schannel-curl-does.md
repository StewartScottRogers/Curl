# ADR-1187: The Schannel build fails a P12 client certificate at AcquireCredentialsHandle, as Schannel curl does

- **Status:** Superseded by ADR-1240
- **Date:** 2026-11-02
- **Supersedes:** ADR-0794, ADR-0826

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3625
(CGR-096 of the third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`).

## Context

ADR-0794 and ADR-0826 kept Curl presenting a PKCS#12 `--cert` file in the Schannel build, where
Schannel curl on Windows fails before sending a ClientHello. On 2026-11-02 Stewart asked for every
existing task, BL-3625 included, to be completed before the third-party handoff, and BL-3625's
acceptance criteria ask for the difference to be removed. The standing rule is to match the
platform's curl.

Measured on 2026-11-02 on this machine: a self-signed RSA-2048 `cli.p12` with password `secret`,
`curl -sS -k --cert cli.p12:secret --cert-type P12 https://localhost:<p>/index.txt`, with and
without `--tls-max 1.2`, against a loopback listener. curl 8.21.0 (System32, Schannel) exits 35 with
`curl: (35) schannel: AcquireCredentialsHandle failed: SEC_E_UNKNOWN_CREDENTIALS (0x8009030d) - The
credentials supplied to the package were not recognized` and sends no byte to the server. Lane 8
measured the same with curl 8.22.0 on 2026-10-26. The reviewer's machine gave exit 35 with other
texts (`SEC_E_INTERNAL_ERROR`, `SEC_E_ALGORITHM_MISMATCH`).

## Decision

In the Schannel build, `ClientCertificateLoader` keeps every earlier check of a PKCS#12 file
(missing, wrong `--cert-type`, empty, not PKCS#12, bad password, all with their own text) and,
once the file opens with its passphrase, fails with exit 35 and this machine's
`AcquireCredentialsHandle` text (`TlsFailureMessages.SchannelClientCertificateCredentialsUnknown`)
before any ClientHello. Exit 35 is common to every machine measured, so scripts see the same exit
code; the text is the one measured on the reference machine. The OpenSSL build and a certificate
from a Windows store are unchanged.

## Consequences

- CGR-096 is closed on Windows: exit code, stdout, stderr and the bytes on the wire match.
- A PKCS#12 client certificate no longer works in the Schannel build, as it does not in Schannel
  curl; a user who needs one uses a store path (`CurrentUser\MY\<thumbprint>`), as with Schannel
  curl. Tests that need a presented certificate use the OpenSSL build's `--cert-type P12`.
- The Schannel build now fails on the certificate before it would fail on a bad CA file or an
  untrusted server, as curl's `schannel_connect_step1` acquires credentials before the handshake
  and its CA file is read only to verify the server afterwards.
- If a later Windows or curl release brings back exit 0, this decision is reversed by removing
  the failure in `ClientCertificateLoader.LoadPkcs12AsSchannelBuild`.
