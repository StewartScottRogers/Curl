---
id: BL-560
title: Decide how scp and sftp are built on System.Security.Cryptography alone
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-498, BL-515]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-560 — Decide how scp and sftp are built on System.Security.Cryptography alone

## Goal

An ADR fixes how `Curl.Protocol.Ssh.UnitLibrary` implements SSH-2 for `scp://` and `sftp://` with the base class library only: which key-exchange, host-key, cipher, MAC and compression algorithms Curl offers and in what order, which it cannot offer and what then happens, which private-key file formats `--key` reads, how the handler is structured (transport, user auth, connection/channel, SFTP, SCP), and how tests drive it without a network.

## Context

- Conformance audit 2026-09-28, row 35 (Blocker, L+: SSH project empty; SSH transport and crypto on the BCL) and row 31 (`--pubkey`, `--knownhosts`, `--hostpubmd5`, `--hostpubsha256`, `--compressed-ssh`).
- **BCL only (Stewart's standing rule).** Crypto must come from `System.Security.Cryptography`: `ECDiffieHellman` (NIST P-256/384/521), `BigInteger` for `diffie-hellman-group14-sha256`/`group16-sha512`, `RSA` (rsa-sha2-256/512), `ECDsa`, `Aes` (CTR built on ECB), `AesGcm`, `HMACSHA256`/`HMACSHA512`, `SHA*`, `RandomNumberGenerator`; zlib from `System.IO.Compression.ZLibStream`. Curve25519 key exchange, Ed25519 host keys and user keys, `chacha20-poly1305@openssh.com` and bcrypt-pbkdf (encrypted OpenSSH keys) have no BCL primitive: the ADR decides, for each, whether Curl omits it (and what a server or key that needs it then gets) or hand-writes it in C#, weighing the security risk of hand-written crypto. **If something required truly cannot be built on the BCL, the ADR says so and the dependent task blocks for Stewart; no package is added.**
- Match the platform's curl: record `curl -V` of the reference build (the SSH library it names, libssh2 or libssh, and its version) and which algorithms it offers (run it with `-v` against a local OpenSSH `sshd -ddd` through `Record-CurlExchange.ps1 -NoServer`, BL-528, and read the server's log of the client's KEXINIT).
- Specifications: RFC 4250-4254 (SSH-2), RFC 4344 (CTR), RFC 5647/OpenSSH `aes*-gcm@openssh.com`, RFC 5656 (ECDH, ECDSA), RFC 8268 (DH group14/16 SHA-2), RFC 8332 (rsa-sha2), RFC 8308 (ext-info), draft-ietf-secsh-filexfer-02 (SFTP v3), OpenSSH `PROTOCOL` and `PROTOCOL.key`.
- Depends on BL-498 (timeouts contract) and BL-515 (endpoints). `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md`: Abstractions only, `IConnection`, no `Socket`/`SslStream`.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with the measured reference facts in its Context.
- [ ] The Decision lists the offered algorithm names in order per category, each one's BCL type, the omitted or hand-written ones with the reason, the key-file formats read, the class structure, and the test approach (an in-memory SSH peer in `Curl.Protocol.Ssh.UnitTests` built from the same BCL primitives with fixed keys and an injected random source).
- [ ] The Decision states the rule that any piece that cannot be built on the BCL blocks its task for Stewart rather than adding a package.
- [ ] Consequences list BL-561 to BL-578 and what each relies on from the ADR.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
