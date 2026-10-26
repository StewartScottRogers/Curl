# ADR-0826: CGR-096's P12 client certificate difference is kept, as ADR-0794 decided

- **Status:** Accepted
- **Date:** 2026-10-26

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3625
(CGR-096 of the third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`).

## Context

CGR-096 reports that on Windows `curl -sS -k --cert cli.p12:secret --cert-type P12
https://localhost:<p>/index.txt` against a server that requires a client certificate exits 0
with Curl. The reviewer's curl 8.22.0 Schannel exits 35, with
`schannel: next InitializeSecurityContext failed: SEC_E_INTERNAL_ERROR (0x80090304) - The Local Security Authority cannot be contacted`,
or `SEC_E_ALGORITHM_MISMATCH (0x80090331)` with `--tls-max 1.2`. ADR-0794 had already looked at
this difference and decided to keep it.

Measured again on 2026-10-26 on the lane machine (Windows 11, build 26300). The server was a
loopback `SslStream` server that requires a client certificate. The client certificate was a
self-signed RSA-2048 PKCS#12 file, password `secret`:

| Client | default | `--tls-max 1.2` |
|---|---|---|
| curl 8.22.0 (Git ucrt64, Schannel) | 35 `curl: (35) schannel: AcquireCredentialsHandle failed: SEC_E_UNKNOWN_CREDENTIALS (0x8009030d) - The credentials supplied to the package were not recognized` | 35, same text |
| curl 8.21.0 (`C:\Windows\System32\curl.exe`) | 35, same text | 35, same text |
| Curl (`Curl.Console`, Debug) | 0, `hello tls`; the server received the certificate | 0, `hello tls` |

The same file, loaded in .NET with `X509KeyStorageFlags.EphemeralKeySet` (no persisted key, as
curl imports it with `PKCS12_NO_PERSIST_KEY`), completes TLS 1.2 and TLS 1.3 client
authentication through `SslStream` on the same machine. So Windows can present this key.
The failure comes from the credential that curl's Schannel backend builds from the imported
file.

## Decision

ADR-0794 stands: on Windows, Curl presents a P12 client certificate and does not reproduce exit 35.

- The failing call and its text depend on the Windows build. The lane machine fails in
  `AcquireCredentialsHandle` with `SEC_E_UNKNOWN_CREDENTIALS` for both TLS versions. The
  reviewer's machine failed later, in `InitializeSecurityContext`, with two different codes
  for the two TLS versions. ADR-0009 measured exit 0 with curl 8.21.0 on this machine on
  2026-09-26. No single text Curl could print would match curl on every Windows machine.
- Copying the failure would make every file-based client certificate fail on Windows. The
  Schannel build reads no PEM `--cert`, so mutual TLS from a file would stop working
  altogether. Curl would be giving up a working feature to match a credential refusal that
  depends on the Windows build.
- The failures curl itself makes still match: a bad password, a missing file, a PEM file and
  a wrong `--cert-type` (ADR-0009, ADR-0794).

## Consequences

- CGR-096 stays a known difference on Windows. BL-3625's acceptance criteria ask for the
  difference to be removed, so the task waits for Stewart to accept the difference or reverse
  this decision.
- If Stewart reverses this decision, the change goes in `ClientCertificateLoader`'s Schannel
  PKCS#12 path: after the password check, return exit 35 with this machine's
  `AcquireCredentialsHandle` text. Every test that presents a P12 file through the Schannel
  build, in Networking, Engine and Console, would then have to expect that failure.
