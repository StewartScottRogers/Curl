# ADR-0066 — A Schannel `--cert` store path is parsed as curl parses it and opened through `X509Store`

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

curl's Schannel build reads a `--cert` value of the form
`<location>\<store name>\<thumbprint>` (for example `CurrentUser\MY\<40 hex digits>`) as a
certificate in a Windows certificate store, and reads anything else as a PKCS#12 file
(`get_cert_location` in `lib/vtls/schannel.c`). BL-065 loaded files only. BL-248 adds the
store form. Measured against curl 8.21.0 (Schannel, Windows) on 2026-09-27, with a
self-signed certificate put in `CurrentUser\MY` for the measurement and removed after:

| `--cert` value | curl 8.21.0 |
| --- | --- |
| `CurrentUser\MY\<thumbprint>` | presents it, exit 0 |
| the same, thumbprint in lower case | presents it |
| `CurrentUser\my\<thumbprint>` | presents it (Windows matches store names ignoring case) |
| `Current\MY\<thumbprint>` | presents it (the location is compared as a prefix) |
| `…\<thumbprint>:secret`, or with `--cert-type PEM` | presents it (neither is read) |
| `currentuser\MY\<thumbprint>` | `schannel: Failed to get certificate location or file for …` (read as a file) |
| `Bogus\MY\<thumbprint>`, `CurrentUser\MY\abc` | the same file message |
| `CurrentUser\MY\<thumbprint not in the store>` | exit 58, `schannel: client cert not found in cert store` |
| `CurrentUser\NOSUCHSTORE\<40 characters>` | exit 58, `schannel: Failed to open cert store 10000 NOSUCHSTORE, last error is 0x00000002` |
| `CurrentUser\MY\zz<38 hex digits>` | exit 58, `Problem with the local SSL certificate` (no message of its own) |

## Decision

- `ClientCertificateStorePath.Parse` follows `get_cert_location`: the text before the first
  backslash selects the first of curl's eight location names (`CurrentUser`, `LocalMachine`,
  `CurrentService`, `Services`, `Users`, `CurrentUserGroupPolicy`, `LocalMachineGroupPolicy`,
  `LocalMachineEnterprise`) it is a case-sensitive prefix of, an empty text included; the text
  up to the second backslash is the store name; what follows must be exactly 40 characters.
  Anything else is not a store path and is read as a file, as before.
- The store is opened first, then the thumbprint is checked as hex, then the certificate is
  found by thumbprint ignoring case, which is the order and the three failures curl has. A
  store path ignores `--cert-type`, `--key`, `--key-type` and the passphrase.
- The store is behind `IClientCertificateStore`, so tests never read the machine's stores.
  The production `SystemClientCertificateStore` opens `CurrentUser` and `LocalMachine` with
  `X509Store` (read-only, existing stores only). The other six locations have no
  `StoreLocation` in the base class library; they report the open failure, with last error
  `0x00000002`, rather than call `CertOpenStore` through P/Invoke.
- The OpenSSL build has no store form and still reads the value as a file.

## Consequences

- The two locations people use with curl, `CurrentUser` and `LocalMachine`, work as curl's
  Schannel build does, with its messages.
- A certificate in one of the six rarer locations is not found, where curl would find it;
  the message names the store as curl would, but the last error is not the one Windows
  would give. Supporting them means P/Invoke to `crypt32.dll`, which is a follow-up if
  anyone asks.
- A thumbprint with spaces in it, which `CryptStringToBinary` might accept, is refused as
  not hex.

## Alternatives considered

- **P/Invoke `CertOpenStore` for every location.** Exact, but platform-specific interop in
  a library that otherwise stays in managed BCL types, for locations almost nobody uses.
- **Read `CurrentUser\MY` only.** Simpler, but a `LocalMachine` path, which the manpage and
  users do write, would fail where curl succeeds.
