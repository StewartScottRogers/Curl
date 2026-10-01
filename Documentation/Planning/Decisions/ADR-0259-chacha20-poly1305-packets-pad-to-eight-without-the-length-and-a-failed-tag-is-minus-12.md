# ADR-0259 — chacha20-poly1305@openssh.com packets pad to eight without the length, and a failed tag is libssh2's -12

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-679.
Builds on ADR-0122 (the algorithm lists) and ADR-0212 (the `ISshPacketProtection` seam and
the codes a failed check ends with).

## Context

BL-679 adds `chacha20-poly1305@openssh.com`, the first cipher in both reference builds'
`KEXINIT` lists, as one more `ISshPacketProtection` over `Curl.Cryptography.UnitLibrary`'s
`ChaCha20` and `Poly1305`. OpenSSH's `PROTOCOL.chacha20poly1305` fixes the construction; it
leaves two things for this library to choose: how packets are padded, and what curl prints
when a packet fails its tag.

## Decision

- **The construction is OpenSSH's, exactly.** The 64-byte encryption key (`C` or `D`,
  derived to 64 bytes; no IV is derived) is `K_2` (bytes 0-31) then `K_1` (bytes 32-63).
  The nonce is the sequence number as a 64-bit big-endian integer, with ChaCha's original
  64-bit block counter. `packet_length` is encrypted under `K_1` from block 0; the
  Poly1305 key is the first 32 bytes of block 0 under `K_2`; the rest of the packet is
  encrypted under `K_2` from block 1; the 16-byte tag covers the encrypted length and the
  encrypted rest. No MAC is negotiated beside it (already `IsAuthenticatedEncryption`).
- **The tag is checked before anything but the length is decrypted**, with `Poly1305.Verify`
  (constant-time). The length has to be decrypted first to know where the tag is.
- **Block size 8, and the four length bytes are not padded**: OpenSSH's `packet.c` pads
  and checks `packet_length` alone to the block size whenever the cipher has an
  authentication tag, as for AES-GCM, and libssh2 1.11.1 interoperates with it.
- **A failed tag is `LIBSSH2_ERROR_DECRYPT`, -12**, the AES-GCM code: libssh2 1.11.1's
  `decrypt()` returns it whenever an integrated-MAC cipher refuses a packet. Both reference
  builds were measured on 2026-10-01 (BL-1032, below): neither exits at all, but loops
  allocating memory until it is killed. Curl does not copy that; it ends with -12 and
  exit 2, as the same builds do for a failed AES-GCM tag (ADR-0212). Decided by Claude
  under Stewart's delegation: a run that only ends when the kernel kills it, with no
  output, `-m` ignored and the machine's memory exhausted, is a libssh2 defect no script
  can depend on, and a drop-in replacement must not take a machine down; -12 is the code
  libssh2's integrated-MAC path is written to return.
- **A corrupted length** decrypts to a different `packet_length`. One off the block size or
  over the maximum is the framing failure ADR-0206 maps (`-8`, exit 2); one that happens to
  frame fails its tag (`-12`) or runs out of bytes, as the peer would see it. Both reference
  builds ended a chacha20-poly1305 length over the maximum with -41 instead (below); BL-1032
  filed matching that as its own task.

## Measured 2026-10-01 (BL-1032)

Both reference builds, run as `curl -sS -k -u u:p sftp://<host>:<port>/x` against a throwaway
MSTest method (deleted after the run) that bridged a `TcpListener` to
`Fakes.InMemorySshServer` with `Cipher = "chacha20-poly1305@openssh.com"` and altered one
byte of the server's first packet after `NEWKEYS` (`SERVICE_ACCEPT`, sequence number 3):
the OpenSSL build as `curlimages/curl:8.21.0` under Docker Desktop (`OpenSSL/3.5.7`,
`libssh2/1.11.1`, against `host.docker.internal`), and the Windows build as Git for
Windows' `mingw64\bin\curl.exe` (`curl 8.21.0 (x86_64-w64-mingw32) ... Schannel ...
libssh2/1.11.1`, against `127.0.0.1`).

| Server's `SERVICE_ACCEPT` | OpenSSL build | Windows build |
| --- | --- | --- |
| Unaltered | exit 78, `curl: (78) Could not open remote file for reading: No such file or directory` | the same |
| Last tag byte flipped | no output, never exits; one core at 100%, memory grows about 160 MiB/s until the kernel kills it (`OOMKilled=true`, exit 137, after 4.5 to 10 minutes, 30 to 60 GiB) | no output, never exits; one core at 100%, memory grows about 240 MiB/s (4.3 GiB at 18 s, when the harness killed it) |
| Ciphertext byte 8 flipped (inside the encrypted payload; the length is bytes 0-3) | the same as the tag | the same as the tag |
| Tag flipped, with `-m 10` or `--connect-timeout 5` | the same: neither option ends it | not run |
| Tag flipped, the server closing the TCP connection 20 s later | the same: the close does not end it | not run |
| Length's top bit flipped (`packet_length` over the maximum) | exit 2, `curl: (2) Failure establishing ssh session: -41, Failed to get response to ssh-userauth request` | the same |

After an altered tag or payload curl sent no further byte and printed nothing, even under
`-v` (its last line was `* SSH: user 'u'`). The loop is inside libssh2 1.11.1's read of
the packet, which is why curl's own timeouts never run.

## Consequences

- Against an OpenSSH server both presets now agree `chacha20-poly1305@openssh.com`, their
  first choice, where they agreed AES-GCM (OpenSSL) or AES-CTR with HMAC-SHA-256 (Windows).
- The hand-built ChaCha20 is slower than the BCL's AES; a transfer is bound by the
  connection, not the cipher, at the sizes curl moves.

## Alternatives considered

- **Pad including the length, as RFC 4253 frames packets.** OpenSSH would refuse such a
  packet as off the block size; rejected.
- **The BCL's `ChaCha20Poly1305`.** It is RFC 8439's AEAD, with a 12-byte nonce, a
  length-padded MAC input and no separate length key; OpenSSH's construction cannot be
  built from it. Rejected for the raw primitives BL-673 provides.
