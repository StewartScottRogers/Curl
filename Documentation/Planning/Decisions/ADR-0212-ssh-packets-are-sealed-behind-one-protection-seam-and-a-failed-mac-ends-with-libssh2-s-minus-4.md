# ADR-0212 — SSH packets are sealed behind one protection seam, and a failed MAC ends the session with libssh2's -4

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-565.
Builds on ADR-0122 (which algorithms, and the `PacketProtection` folder) and ADR-0206 (the
keys `NEWKEYS` hands out).

## Context

BL-565 encrypts and authenticates packets after `NEWKEYS` with ADR-0122's AES-GCM, AES-CTR
and SHA-2 MAC rows, in both MAC orders, and must end the session as curl 8.21.0 does when a
packet's MAC or tag fails. ADR-0122 did not say what curl prints then, nor how the packet
layer is shaped so BL-679 (`chacha20-poly1305@openssh.com`) and BL-680 (the CBC, 3DES,
Blowfish, CAST-128, arcfour, SHA-1, MD5 and RIPEMD-160 rows) add algorithms without
reshaping it.

### Measured 2026-09-29

The Windows reference build (`curl 8.21.0 ... libssh2/1.11.1`, WinCNG), run as
`curl -sS -k -u u:p sftp://127.0.0.1:<port>/x` against a throwaway loopback server built
from this library's own classes in the server role (group14-sha256, `rsa-sha2-256`, then
the cipher and MAC under test; the server was an MSTest method, deleted after the run):

| Server's first packet after `NEWKEYS` (`SERVICE_ACCEPT`) | Exit | stderr |
| --- | --- | --- |
| Correct, for each of `aes256-ctr`, `aes192-ctr`, `aes128-ctr` with each of `hmac-sha2-256`, `hmac-sha2-256-etm@openssh.com`, `hmac-sha2-512`, `hmac-sha2-512-etm@openssh.com` | 79 | `curl: (79) Error in the SSH layer` (the server closed after curl's `USERAUTH_REQUEST`, which it decrypted and verified) |
| Last MAC byte flipped, `aes128-ctr` + `hmac-sha2-256` | 2 | `curl: (2) Failure establishing ssh session: -4, Failed to get response to ssh-userauth request` |
| One ciphertext byte flipped, `aes256-ctr` + `hmac-sha2-256` | 2 | same |
| Last MAC byte flipped, `aes128-ctr` + `hmac-sha2-512-etm@openssh.com` | 2 | same |
| One ciphertext byte flipped, `aes256-ctr` + `hmac-sha2-512-etm@openssh.com` | 2 | same |
| The server closed instead of answering | 2 | `curl: (2) Failure establishing ssh session: -43, Failed to get response to ssh-userauth request` |

All twelve CTR pairs interoperate with libssh2 in both directions. The Windows build offers
no AES-GCM, and the OpenSSL build could not be reached the same day (WSL's curl could not
connect to the Windows listener; Docker was down).

### Measured 2026-09-30 (BL-897)

The OpenSSL reference build (`curlimages/curl:8.21.0` under Docker Desktop: `curl 8.21.0
(x86_64-pc-linux-musl) ... OpenSSL/3.5.7 ... libssh2/1.11.1`), run as
`curl -sS -k -u u:p sftp://host.docker.internal:<port>/x` against a throwaway MSTest
method that bridged a `TcpListener` to `Fakes.InMemorySshServer` (the same group14-sha256
and `rsa-sha2-256` session) and altered one byte of the server's first packet after
`NEWKEYS` on its way out (deleted after the run):

| Server's first packet after `NEWKEYS` (`SERVICE_ACCEPT`) | Exit | stderr |
| --- | --- | --- |
| Correct, `aes256-gcm@openssh.com` | 78 | `curl: (78) Could not open remote file for reading: No such file or directory` (the session ran to the SFTP open) |
| Last tag byte flipped, `aes256-gcm@openssh.com` | 2 | `curl: (2) Failure establishing ssh session: -12, Failed to get response to ssh-userauth request` |
| One ciphertext byte flipped, `aes256-gcm@openssh.com` | 2 | same |
| Last tag byte flipped, `aes128-gcm@openssh.com` | 2 | same |
| One ciphertext byte flipped, `aes128-gcm@openssh.com` | 2 | same |
| Last MAC byte flipped, `aes128-ctr` + `hmac-sha2-256` | 2 | `curl: (2) Failure establishing ssh session: -4, Failed to get response to ssh-userauth request` |

The GCM code, -12, is confirmed, and the OpenSSL build agrees with the Windows build's -4
for a failed MAC. `chacha20-poly1305@openssh.com` (ADR-0259) interoperated unaltered, but
with its tag or ciphertext altered curl printed nothing and did not exit until the
container was killed; BL-1032 measures that case.

## Decision

- **One seam, `ISshPacketProtection`,** per direction: `Seal` a framed packet;
  `DecryptPacketLength` of the first `LengthBlockLength` bytes; `Open` the rest and its
  `TagLength` bytes of MAC or tag. `BlockSize` and `PadsPacketLengthField` tell the writer
  how to pad and the reader how to check the framing. `SshPlainPacketProtection` is the
  `none` cipher both directions use until the first `NEWKEYS`;
  `CipherAndMacPacketProtection` pairs an `ISshCipher` (today `AesCtrSshCipher`, over
  `Curl.Cryptography.AesCtr`) with an `SshMac` in either order; `AesGcmPacketProtection`
  is the BCL's `AesGcm` with RFC 5647's nonce (a four-byte fixed field and an eight-byte
  invocation counter that wraps) and the length as associated data. BL-679 adds one more
  implementation; BL-680 adds `ISshCipher` implementations and `SshMac` rows (a truncated
  MAC is a shorter `Length`). `SshPacketProtections` maps names to key and IV lengths, and
  `SshAlgorithmCatalogue.Implemented` offers what it holds.
- **Each direction switches at its own `NEWKEYS`**: the client's packets once it has sent
  its `NEWKEYS`, the server's once the server's has arrived (RFC 4253 section 7.3). Both
  protections are built before the client's `NEWKEYS` is sent, so an unimplemented name
  fails before anything is encrypted.
- **Padding is the fewest bytes, at least four,** that align the padded part to the
  cipher's block size, as libssh2 1.11.1 pads; under encrypt-then-MAC and AES-GCM the four
  length bytes are not part of the padded part. The reader refuses a zero length and a
  length off the block size with the framing failures ADR-0206 already maps.
- **A failed MAC is `LIBSSH2_ERROR_INVALID_MAC`, -4, and a failed AES-GCM tag is
  `LIBSSH2_ERROR_DECRYPT`, -12**, carried by `SshPacketAuthenticationException`. MACs are
  compared with `CryptographicOperations.FixedTimeEquals`, and under encrypt-then-MAC the
  MAC is checked before anything is decrypted. The -4 is measured; the -12 is taken from
  libssh2 1.11.1's `transport.c` (`decrypt()` returns `LIBSSH2_ERROR_DECRYPT` when the
  cipher refuses, and its OpenSSL AES-GCM refuses a bad tag), and BL-897 measured it on
  the OpenSSL build, which prints -12 for a bad tag and for altered ciphertext alike.
- **libssh2 keeps the failed read's code and names the step in the description.** The
  first packet the server protects answers the `ssh-userauth` service request, so curl
  prints `-4, Failed to get response to ssh-userauth request`
  (`Libssh2ErrorCode.FailedToGetUserAuthResponse`); BL-567, which sends that request,
  maps the exception to it. Within this task the only protected reads are a server's
  re-exchange, which ends with exit 2 and `-4` (or `-12`), `Unable to exchange encryption
  keys` - the code from the read and the description of the step, the same pattern the
  measured case shows.

## Consequences

- Real curl decrypts what this library encrypts and the reverse, for every CTR pair, so
  BL-567 onwards can be measured against a server built from these classes.
- The GCM exit is measured (BL-897) and matches the constant, so `Libssh2ErrorCode.Decrypt`
  and its tests stand unchanged.
- A packet that fails its check is not counted, and the session is over: nothing reads
  from the connection after the exception.

## Alternatives considered

- **Fold a MAC failure into ADR-0206's -8.** Simpler, but the measurement shows libssh2
  prints -4, and a drop-in replacement prints what curl prints. Rejected.
- **Separate reader and writer types per cipher.** Two classes per algorithm and the
  framing logic repeated in each; rejected for one seam that both directions share.
- **Hand-build AES-GCM now.** ADR-0118 found the BCL's `AesGcm` supported on all three CI
  platforms with a 16-byte tag, which is the only size SSH uses. Not needed.
