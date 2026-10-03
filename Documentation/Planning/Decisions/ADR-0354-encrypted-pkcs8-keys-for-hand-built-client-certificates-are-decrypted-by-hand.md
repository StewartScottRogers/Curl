# ADR-0354 — Encrypted PKCS #8 keys for hand-built client certificates are decrypted by hand

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1085.

## Context

ADR-0301 reads an Ed25519, Ed448 or ML-DSA `--key` by hand with `HandBuiltPrivateKeyReader`,
from the first unencrypted `PRIVATE KEY` PEM block only, so an `ENCRYPTED PRIVATE KEY` with
`--pass` gave exit 43 where OpenSSL-built curl decrypts it and presents the certificate. The
BCL decrypts PKCS #8 only while importing it into a key type it holds itself, and
`Pkcs8PrivateKeyInfo` lives in `System.Security.Cryptography.Pkcs`, a package.
`Curl.Protocol.Ssh.UnitLibrary` has a decryptor, but Networking may not reference a protocol
library.

## Decision

1. `EncryptedPrivateKeyInfoDecryption` in `Curl.Networking.UnitLibrary` decrypts an
   `EncryptedPrivateKeyInfo` with `System.Formats.Asn1`, `Rfc2898DeriveBytes.Pbkdf2` and the
   BCL's CBC ciphers: PBES2 with PBKDF2 over HMAC-SHA-1, -256, -384 or -512 (SHA-1 when the
   parameters name no PRF; an optional key length is read and the cipher's own used) and
   AES-128, -192, -256 or DES-EDE3 in CBC mode. That is what `openssl pkcs8 -topk8` and
   `openssl genpkey -aes256` write and what OpenSSL 3's default provider decrypts.
2. PBES1 and single DES are refused: OpenSSL 3 decrypts them only with its legacy provider,
   which curl does not load.
3. The passphrase's UTF-8 bytes are PBKDF2's password, as OpenSSL takes `--pass`'s bytes.
4. In a PEM `--key`, the first block labelled `PRIVATE KEY` is used, or, when a passphrase is
   given, the first labelled `ENCRYPTED PRIVATE KEY`, whichever comes first. Without a
   passphrase an encrypted block is skipped (curl would prompt on a terminal; Curl does not
   prompt), so the key is unusable.
5. A malformed structure, an unsupported scheme and a wrong passphrase are all a
   `CryptographicException`, so they reach the exit 43 `unable to set private key file:
   '<key>' type PEM` an unusable `--key` already gives. A DER `--key` is never decrypted, as
   ADR-0301 says for OpenSSL.

## Consequences

An encrypted key now loads for every hand-built key algorithm with no package and no
reference to the SSH library. The SSH library keeps its own decryptor, which also takes PBES1
because OpenSSH key files need it; the two are small enough that sharing them through a new
library would cost more than it saves.

## Alternatives considered

- Reference `System.Security.Cryptography.Pkcs`: a package, which needs Stewart's approval.
- Move the SSH decryptor to a shared library: a cross-project change outside this task's
  `touches` for about a hundred lines.
- Import through `ECDsa.ImportEncryptedPkcs8PrivateKey` and similar: the BCL refuses the
  algorithm before handing back the decrypted bytes.
