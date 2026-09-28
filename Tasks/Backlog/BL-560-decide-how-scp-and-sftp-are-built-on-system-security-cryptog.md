---
id: BL-560
title: Decide how scp and sftp are built with every algorithm curl's SSH backends offer
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-498, BL-515]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-560 — Decide how scp and sftp are built with every algorithm curl's SSH backends offer

## Goal

An ADR fixes how `Curl.Protocol.Ssh.UnitLibrary` implements SSH-2 for `scp://` and `sftp://` on every platform: every key-exchange, host-key, cipher, MAC and compression algorithm curl's SSH backends offer and in what order (the default-enabled set and the full set), which BCL type or hand-built `Curl.Cryptography.UnitLibrary` primitive each uses, which private-key file formats `--key` reads (all of them, encrypted `openssh-key-v1` and Ed25519 included), how the handler is structured (transport, user auth, connection/channel, SFTP, SCP), and how tests drive it without a network.

## Context

- Conformance audit 2026-09-28, row 35 (Blocker, L+: SSH project empty; SSH transport and crypto on the BCL) and row 31 (`--pubkey`, `--knownhosts`, `--hostpubmd5`, `--hostpubsha256`, `--compressed-ssh`).
- **Offer everything (Stewart's standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28).** A complete reimplementation of curl: nothing is omitted because it is hard or because the BCL lacks it, and no task blocks for a missing primitive. What the BCL has comes from `System.Security.Cryptography` (`ECDiffieHellman` NIST P-256/384/521, `BigInteger` for the finite-field groups, `RSA`, `DSA`, `ECDsa`, `Aes` with CTR built on ECB and CBC, `AesGcm`, `TripleDES`, `HMACSHA1`/`256`/`512`, `HMACMD5`, `SHA*`, `RandomNumberGenerator`), zlib from `System.IO.Compression.ZLibStream`; what it lacks is hand-built in `Curl.Cryptography.UnitLibrary` (BL-669's ADR): X25519 (BL-671), Ed25519 (BL-672), ChaCha20 and Poly1305 (BL-673), Blowfish and bcrypt-pbkdf (BL-674), RIPEMD-160 (BL-675), RC4 and CAST-128 (BL-676). Any further primitive this ADR finds is added to BL-669's ADR and filed as a task, never omitted. `Curl.Protocol.Ssh.UnitLibrary` may reference `Curl.Cryptography.UnitLibrary` (BL-667, BL-668).
- The set to offer is the union of what curl's SSH backends offer: libssh2 1.11.1 (curl.se's official Windows build of curl 8.22.0 uses it, https://curl.se/windows/, checked 2026-09-28; https://libssh2.org/ lists host keys `ssh-ed25519`, `ssh-ed25519-cert-v01@openssh.com`, `ecdsa-sha2-nistp521/384/256`, `ssh-rsa`, `ssh-dss`; ciphers `aes256-gcm@openssh.com`, `aes128-gcm@openssh.com`, `aes256/192/128-ctr`, `aes256/192/128-cbc`, `rijndael-cbc@lysator.liu.se`, `3des-cbc`, `blowfish-cbc`, `cast128-cbc`, `arcfour`, `arcfour128`; MACs `hmac-sha2-512/256` and their `-etm@openssh.com` forms, `hmac-sha1`, `hmac-sha1-96`, `hmac-md5`, `hmac-md5-96`, `hmac-ripemd160`; compression `zlib`, `zlib@openssh.com`, `none`; plus its key exchanges, which that page lists incompletely: read libssh2 1.11.1's `src/kex.c` for `curve25519-sha256`, `ecdh-sha2-nistp*`, the `diffie-hellman-group*` and `group-exchange` methods) and libssh (curl's other SSH backend, which adds `chacha20-poly1305@openssh.com`; record its version's list from its documentation or source). Offer order and the default-enabled subset match the platform's reference build where it has SSH, and libssh2's defaults otherwise.
- Measure the platform's curl: record `curl -V` of the reference build (the SSH library it names and its version) and which algorithms it offers (run it with `-v` against a local OpenSSH `sshd -ddd` through `Record-CurlExchange.ps1 -NoServer`, BL-528, and read the server's log of the client's KEXINIT), and do the same with curl.se's official Windows build.
- Specifications: RFC 4250-4254 (SSH-2), RFC 4344 (CTR), RFC 5647/OpenSSH `aes*-gcm@openssh.com`, RFC 5656 (ECDH, ECDSA), RFC 8268 (DH group14/16 SHA-2), RFC 8332 (rsa-sha2), RFC 8308 (ext-info), draft-ietf-secsh-filexfer-02 (SFTP v3), OpenSSH `PROTOCOL` and `PROTOCOL.key`.
- Depends on BL-498 (timeouts contract) and BL-515 (endpoints). `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md`: `IConnection`, no `Socket`/`SslStream`; it references Abstractions and, once BL-668 lands, `Curl.Cryptography.UnitLibrary`.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measured reference facts in its Context.
- [ ] The Decision lists every offered algorithm name per category, in order, marking the default-enabled ones, with each one's BCL type or hand-built primitive and the task that builds it; no algorithm curl's SSH backends offer is omitted.
- [ ] It lists the key-file formats read (PEM PKCS#1/PKCS#8/SEC1, encrypted PEM, `openssh-key-v1` unencrypted and bcrypt-encrypted, for RSA, DSA, ECDSA and Ed25519), the class structure, and the test approach (an in-memory SSH peer in `Curl.Protocol.Ssh.UnitTests` built from the same primitives with fixed keys and an injected random source).
- [ ] The Decision states that a primitive the BCL lacks is hand-built in `Curl.Cryptography.UnitLibrary`, never a package and never a blocked task.
- [ ] Consequences list BL-561 to BL-578 and BL-678 to BL-681 and what each relies on from the ADR.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
