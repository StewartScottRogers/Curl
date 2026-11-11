# ADR-1240: The Schannel build presents a P12 client certificate file, as curl 8.22.0 Schannel does

- **Status:** Accepted
- **Date:** 2026-11-11
- **Supersedes:** ADR-1187 (and with it ADR-0794 and ADR-0826, which ADR-1187 superseded)

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-4905
(finding STD-002 of the outside fitness review of 2026-11-11).

## Context

ADR-1187 made the Schannel build fail every PKCS#12 `--cert` file that opens with its passphrase
with exit 35 and `SEC_E_UNKNOWN_CREDENTIALS`, because curl 8.21.0 and 8.22.0 Schannel did so on the
machines measured on 2026-10-26 and 2026-11-02. The fitness review of 2026-11-11 found the
reference curl 8.22.0 Schannel now exits 0 with the same command line, and Curl's unconditional
exit 35 is a difference scripts can see.

Measured on 2026-11-11 on this machine (Windows 11, curl 8.22.0 Schannel from Git for Windows, and
the System32 curl), with a CA, a server certificate for `localhost` and `127.0.0.1` and a clientAuth
certificate exported by `openssl pkcs12 -export ... -passout pass:secret`, against `openssl s_server`
on loopback:

- A server that does not request a client certificate:
  `curl -sS https://localhost:<p>/ --cacert ca.pem --ssl-no-revoke --cert client.p12:secret --cert-type P12 -w "%{http_code}\n"`
  exits 0 and prints the body and `200`. AcquireCredentialsHandle accepts the imported key.
- The same with a wrong passphrase: exit 58, `schannel: Failed to import cert file client.p12, password is bad`.
- A server that requires a client certificate: this machine's Schannel fails the handshake after the
  ClientHello (exit 35, `SEC_E_INTERNAL_ERROR` under TLS 1.3, `SEC_E_ALGORITHM_MISMATCH` under
  TLS 1.2), a fault of this machine's LSA with an ephemeral imported key, not a refusal of the file.

## Decision

In the Schannel build `ClientCertificateLoader` keeps every earlier check of a PKCS#12 file (missing,
wrong `--cert-type`, empty, not PKCS#12, bad password, each with its own text) and, once the file
opens with its passphrase, returns the certificate with its key, which `SslStreamTlsProvider`
presents when the server asks for one. Where the server asks, Curl presents the certificate rather
than copy the machine-dependent handshake failure, since that is what the option is for and what
the review's reference machine expects. `TlsFailureMessages.SchannelClientCertificateCredentialsUnknown`
is removed. The OpenSSL build and a certificate from a Windows store are unchanged.

## Consequences

- `--cert client.p12:secret --cert-type P12` works in the Schannel build: exit 0 against a server
  that does not ask, and the certificate is seen by one that does (measured with Curl against
  `openssl s_server -Verify 1`, TLS 1.3 and TLS 1.2: `Subject: CN=client`).
- CGR-096's difference is closed the other way: Curl now matches a Schannel curl whose
  AcquireCredentialsHandle accepts the key.
