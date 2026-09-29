---
id: BL-565
title: Encrypt and authenticate SSH packets with the negotiated cipher and MAC
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-564, BL-737, BL-668]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0212-ssh-packets-are-sealed-behind-one-protection-seam-and-a-failed-mac-ends-with-libssh2-s-minus-4.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-565 — Encrypt and authenticate SSH packets with the negotiated cipher and MAC

## Goal

After `NEWKEYS`, packets are encrypted and authenticated with the ciphers and MACs BL-560's ADR offers (`aes128-ctr`/`aes192-ctr`/`aes256-ctr`, `aes128-gcm@openssh.com`/`aes256-gcm@openssh.com`, `hmac-sha2-256`/`hmac-sha2-512` and their `-etm@openssh.com` forms; `chacha20-poly1305@openssh.com` is BL-679 and the CBC, 3DES, Blowfish, CAST-128, arcfour, SHA-1, MD5 and RIPEMD-160 algorithms are BL-680), and a packet whose MAC or tag fails ends the session as curl 8.21.0 ends it.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-564's keys.
- **BCL only.** `AesCtr` from `Curl.Cryptography.UnitLibrary` (BL-737, ADR-0118: CTR is built once there on the BCL's `Aes.EncryptEcb`, not in the SSH library), `AesGcm`, `HMACSHA256`, `HMACSHA512`, `CryptographicOperations.FixedTimeEquals` for MAC comparison. If `AesGcm` is unsupported on a CI platform, use the fallback BL-669's ADR names (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28: hand-built in `Curl.Cryptography.UnitLibrary`, never a package, never a task blocked for a missing primitive). Structure the packet layer so BL-679 and BL-680 add ciphers and MACs without reshaping it.
- Test vectors: NIST SP 800-38A (CTR) and SP 800-38D (GCM) vectors for the primitives; round trips against the in-memory peer for the packet layer.

## Acceptance criteria

- [x] `Curl.Protocol.Ssh.UnitTests` pin each cipher and MAC against published vectors, round-trip packets of several lengths for each negotiated pair, and show a corrupted MAC or tag ending the session with the exit code BL-560's ADR states.
- [x] Encrypt-then-MAC and MAC-then-encrypt orderings are both covered.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Design (ADR-0212).** New folder `PacketProtection`: `ISshPacketProtection` (one per
  direction: `Seal`, `DecryptPacketLength`, `Open`, plus `BlockSize`,
  `PadsPacketLengthField`, `LengthBlockLength`, `TagLength`), `SshPlainPacketProtection`
  (before the first `NEWKEYS`), `CipherAndMacPacketProtection` (`ISshCipher` +
  `SshMac`, MAC-then-encrypt or encrypt-then-MAC), `AesCtrSshCipher` (over
  `Curl.Cryptography.AesCtr`), `AesGcmPacketProtection` (BCL `AesGcm`, RFC 5647 nonce),
  `SshPacketProtections` (name -> key/IV lengths and factory), and
  `SshPacketAuthenticationException` (carries libssh2's code). `SshPacketReader` and
  `SshPacketWriter` gained `ChangeProtection`; `SshTransport` builds both directions'
  protections before its `NEWKEYS` and switches each direction at its own `NEWKEYS`. The
  catalogue now offers the five AES ciphers and four SHA-2 MACs.
- **Measured 2026-09-29, Windows reference build** (curl 8.21.0, libssh2 1.11.1 WinCNG),
  `curl -sS -k -u u:p sftp://127.0.0.1:<port>/x` against a throwaway MSTest server built
  from this library's classes (deleted before commit): all 12 CTR x SHA-2 pairs
  interoperate both ways (curl decrypted `SERVICE_ACCEPT`; we decrypted its
  `USERAUTH_REQUEST`). A flipped MAC byte or ciphertext byte, under `hmac-sha2-256` and
  `hmac-sha2-512-etm@openssh.com`: exit 2, `curl: (2) Failure establishing ssh session:
  -4, Failed to get response to ssh-userauth request`. Server closing instead: exit 2,
  `-43, Failed to get response to ssh-userauth request`.
- **Exit code.** BL-560's ADR (ADR-0122) states none for this case; ADR-0212 records the
  measured `-4` (MAC) and `-12` for an AES-GCM tag, the latter from libssh2 1.11.1's
  `transport.c` since the Windows build offers no GCM and WSL's OpenSSL curl could not
  reach the Windows listener (Docker down). Filed **BL-889** to measure it. The
  `ssh-userauth` request is BL-567's; it maps `SshPacketAuthenticationException` to
  `Libssh2ErrorCode.FailedToGetUserAuthResponse`. Here a failed check during a
  re-exchange ends with exit 2 and `-4` / `-12, Unable to exchange encryption keys`.
- **Vectors.** AES-CTR: SP 800-38A F.5.1/F.5.3/F.5.5. HMAC-SHA-256/512: RFC 4231 test
  case 2 (its "what" is the sequence number). AES-GCM: SP 800-38D test case 4 on the BCL
  primitive; the SSH framing (4-byte length as AAD) has no published vector, so the
  protection is pinned against the primitive with RFC 5647's nonce, including the
  invocation counter's wrap.
- **Touches.** Added the ADR file and `Documentation/Planning/Decisions/README.md`: the
  task's decision needed an ADR, and no task in Doing names either.
- **Padding** follows libssh2 1.11.1: the fewest bytes, at least four, aligning the padded
  part to the cipher's block (16 for AES); the length field is outside the padded part
  under encrypt-then-MAC and AES-GCM.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Packets are sealed and opened with AES-CTR + HMAC-SHA2 (both orderings) and AES-GCM; a failed MAC or tag ends the session with libssh2's -4/-12; build clean, fast tests green, SSH library at 100/100.
