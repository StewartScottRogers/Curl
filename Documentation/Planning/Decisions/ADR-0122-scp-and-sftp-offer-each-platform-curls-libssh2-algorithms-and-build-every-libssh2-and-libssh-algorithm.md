# ADR-0122 — scp and sftp offer each platform curl's libssh2 algorithms, in its order, and build every algorithm libssh2 and libssh offer

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-560.
Amends ADR-0118's list of hand-built primitives (one row added, sntrup761).

## Context

`Curl.Protocol.Ssh.UnitLibrary` is empty (conformance audit 2026-09-28, row 35) and
`--pubkey`, `--knownhosts`, `--hostpubmd5`, `--hostpubsha256` and `--compressed-ssh`
are unknown options (row 31). Before the SSH tasks (BL-561 to BL-578, BL-678 to BL-681)
can start, they need one answer to: which algorithms, in which order, from which
primitive, reading which key files, in which classes, tested how.

The standing rules are: match the platform's curl (the Schannel build on Windows, the
OpenSSL build on Linux and macOS, ADR-0009), measure it before pinning anything, base
class library only, and leave nothing out because it is hard (root `CLAUDE.md`,
"Decisions", 2026-09-28). What the BCL lacks on any CI platform is hand-built once in
`Curl.Cryptography.UnitLibrary` (ADR-0118), which `Curl.Protocol.Ssh.UnitLibrary` may
reference (ADR-0120).

### The reference builds, measured 2026-09-28

- **Windows:** `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2
  brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the
  mingw build, ADR-0018). `-v` names its crypto backend: `* SSH: libssh2 cryptography
  backend: WinCNG`. curl.se's official Windows build of curl 8.22.0 names the same
  libssh2 1.11.1 (https://curl.se/windows/, checked 2026-09-28).
- **Linux and macOS:** `curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/3.5.7
  ... libssh2/1.11.1 nghttp2/1.69.0 mit-krb5/1.22.2` (the `curlimages/curl:8.21.0`
  image, the OpenSSL build of 8.21.0). `-v`: `* SSH: libssh2 cryptography backend:
  openssl compatible`.
- **libssh, curl's other SSH backend:** Ubuntu 24.04's `curl 8.5.0 ... libssh/0.10.6/openssl/zlib`
  and Fedora's `curl 8.18.0 ... libssh/0.12.2/openssl/zlib`. Neither is a reference
  build; they were measured to find what libssh adds to the full set.

Method: each curl was run with `-v -k -m 5 sftp://127.0.0.1:<port>/x` against a
listener that answers `SSH-2.0-OpenSSH_9.9\r\n` and records what curl sends
(`Record-CurlExchange.ps1 -Response 'SSH-2.0-OpenSSH_9.9\r\n' -HoldOpenMilliseconds 3000`
on Windows; `nc -l` inside the container on Linux), and the client's `SSH_MSG_KEXINIT`
name-lists were decoded from the recorded bytes. `--compressed-ssh` was measured the
same way on `scp://`. libssh 0.12.2's supported (not only default) lists were read from
the strings in `libssh.so.4.12.0`.

**Identification string** (both reference builds): `SSH-2.0-libssh2_1.11.1\r\n`, and
the `KEXINIT` follows at once, before the server's `KEXINIT` arrives.

**Windows reference `KEXINIT`** (WinCNG, no `--compressed-ssh`):

- kex: `diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,diffie-hellman-group14-sha256,diffie-hellman-group14-sha1,diffie-hellman-group1-sha1,diffie-hellman-group-exchange-sha1,ext-info-c,kex-strict-c-v00@openssh.com`
- host key: `rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com`
- ciphers (both directions): `chacha20-poly1305@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,arcfour128,arcfour,3des-cbc`
- MACs (both directions): `hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96`
- compression: `none`; with `--compressed-ssh`: `zlib,zlib@openssh.com,none`
- languages: empty

**Linux and macOS reference `KEXINIT`** (OpenSSL backend):

- kex: `curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,diffie-hellman-group-exchange-sha256,diffie-hellman-group16-sha512,diffie-hellman-group18-sha512,diffie-hellman-group14-sha256,diffie-hellman-group14-sha1,diffie-hellman-group1-sha1,diffie-hellman-group-exchange-sha1,ext-info-c,kex-strict-c-v00@openssh.com`
- host key: `ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521,ecdsa-sha2-nistp256-cert-v01@openssh.com,ecdsa-sha2-nistp384-cert-v01@openssh.com,ecdsa-sha2-nistp521-cert-v01@openssh.com,ssh-ed25519,ssh-ed25519-cert-v01@openssh.com,rsa-sha2-512,rsa-sha2-256,rsa-sha2-512-cert-v01@openssh.com,rsa-sha2-256-cert-v01@openssh.com,ssh-rsa,ssh-rsa-cert-v01@openssh.com`
- ciphers: `chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,blowfish-cbc,arcfour128,arcfour,cast128-cbc,3des-cbc`
- MACs: `hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96,hmac-ripemd160,hmac-ripemd160@openssh.com`
- compression: `none`; with `--compressed-ssh`: `zlib,zlib@openssh.com,none`

**libssh adds** (0.12.2's supported lists, beyond the Linux reference): key exchanges
`mlkem768x25519-sha256`, `mlkem768nistp256-sha256`, `mlkem1024nistp384-sha384`,
`sntrup761x25519-sha512`, `sntrup761x25519-sha512@openssh.com`; host keys
`sk-ssh-ed25519@openssh.com`, `sk-ecdsa-sha2-nistp256@openssh.com`,
`sk-ssh-ed25519-cert-v01@openssh.com`, `sk-ecdsa-sha2-nistp256-cert-v01@openssh.com`; MAC
`hmac-md5-etm@openssh.com`. libssh offers no cipher or compression method the Linux
reference lacks. libssh2 1.11.1 also has `ssh-dss`, compiled out unless the build sets
`LIBSSH2_DSA_ENABLE`; neither reference build offers it.

**Known-hosts narrowing** (Windows, measured with `--knownhosts` files holding one entry
for `[127.0.0.1]:<port>`): curl replaces the host-key list with the type of the entry
that matches the host, as its `lib/vssh/libssh2.c` does before the handshake. An
`ssh-rsa` entry makes the list `rsa-sha2-256,rsa-sha2-512,ssh-rsa`. An `ssh-ed25519`
entry, a type the WinCNG build cannot use, fails before any `KEXINIT` with exit 79 and
`curl: (79) libssh2 method 'ssh-ed25519' failed: The requested method(s) are not
currently supported`. An entry for another host leaves the list unchanged.

**Failures measured** (both reference builds unless marked):

- The server never sends its `KEXINIT`: exit 2, `curl: (2) Failure establishing ssh
  session: -1, Unable to exchange encryption keys`, preceded under `-v` by
  `* Failure establishing ssh session: -1, Unable to exchange encryption keys`.
- No `-k` and no known-hosts file at the default path (Windows): `curl: Could not find a
  known_hosts file` then `curl: (2) Failed initialization`, before any connection. It is
  curl's tool, not libcurl, that checks.

## Decision

### Two default sets, one full set

`Curl.Protocol.Ssh.UnitLibrary` implements **every** algorithm in the full set below.
What its `KEXINIT` offers is an injected `SshAlgorithmPreferences`:

- **`SshAlgorithmPreferences.WindowsReference`**: the Windows reference lists above,
  byte for byte and in that order. `CurlComposition` injects it on Windows.
- **`SshAlgorithmPreferences.OpenSslReference`**: the Linux and macOS reference lists
  above, byte for byte and in that order. `CurlComposition` injects it on Linux and
  macOS.
- **`SshAlgorithmPreferences.Full`**: every algorithm below, in the full-set order.
  Nothing in the console selects it (curl has no option that chooses SSH algorithms);
  tests use it, and it is the one place an algorithm that neither reference offers is
  negotiated.

The full-set order is the OpenSSL reference list, with `ssh-dss` last among the host
keys where libssh2 places it, and each libssh-only name inserted immediately before the
first shared name that follows it in libssh 0.12.2's list (at the end when none
follows or its position is unknown). `ext-info-c` and `kex-strict-c-v00@openssh.com`
always end the key-exchange list.

Every preset sends the identification string `SSH-2.0-libssh2_1.11.1`, sends its
`KEXINIT` straight after the identification exchange with a random 16-byte cookie, an
empty languages list and `first_kex_packet_follows` false, and offers compression
`none` alone unless `--compressed-ssh` is given, when it offers
`zlib,zlib@openssh.com,none`.

When the known-hosts file has an entry for the host, the host-key list is narrowed as
curl narrows it: `ssh-ed25519` to `ssh-ed25519`; `ecdsa-sha2-nistp256/384/521` to that
one name; `ssh-rsa` to `rsa-sha2-256,rsa-sha2-512,ssh-rsa`; `ssh-dss` to `ssh-dss`. A
narrowed name the preset does not hold (on Windows: any ECDSA, Ed25519 or DSA entry)
fails before connecting with exit 79 and `libssh2 method '<name>' failed: The requested
method(s) are not currently supported`, as the WinCNG build does.

### The algorithms

Columns: **W** and **O** are the position in the Windows and OpenSSL reference lists
(blank: not offered there, only in the full set); **Primitive** is where the
cryptography comes from (BCL type, or the `Curl.Cryptography` type and the task that
builds it); **Task** is the SSH task that makes the algorithm work.

#### Key exchange

| # | Name | W | O | From | Primitive | Task |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `mlkem768x25519-sha256` | | | libssh | `MlKem` (BL-743), `X25519` (BL-671), `SHA256` | BL-748 |
| 2 | `mlkem768nistp256-sha256` | | | libssh | `MlKem` (BL-743), `ECDiffieHellman` P-256, `SHA256` | BL-748 |
| 3 | `mlkem1024nistp384-sha384` | | | libssh | `MlKem` (BL-743), `ECDiffieHellman` P-384, `SHA384` | BL-748 |
| 4 | `sntrup761x25519-sha512` | | | libssh | `Sntrup761` (BL-747), `X25519` (BL-671), `SHA512` | BL-748 |
| 5 | `sntrup761x25519-sha512@openssh.com` | | | libssh | as 4 | BL-748 |
| 6 | `curve25519-sha256` | | 1 | libssh2 | `X25519` (BL-671), `SHA256` | BL-678 |
| 7 | `curve25519-sha256@libssh.org` | | 2 | libssh2 | as 6 | BL-678 |
| 8 | `ecdh-sha2-nistp256` | | 3 | libssh2 | `ECDiffieHellman` P-256, `SHA256` | BL-564 |
| 9 | `ecdh-sha2-nistp384` | | 4 | libssh2 | `ECDiffieHellman` P-384, `SHA384` | BL-564 |
| 10 | `ecdh-sha2-nistp521` | | 5 | libssh2 | `ECDiffieHellman` P-521, `SHA512` | BL-564 |
| 11 | `diffie-hellman-group-exchange-sha256` | 1 | 6 | libssh2 | `FiniteFieldDiffieHellman` (BL-739), `SHA256` | BL-564 |
| 12 | `diffie-hellman-group16-sha512` | 2 | 7 | libssh2 | `FiniteFieldDiffieHellman` (BL-739), `SHA512` | BL-564 |
| 13 | `diffie-hellman-group18-sha512` | 3 | 8 | libssh2 | `FiniteFieldDiffieHellman` (BL-739), `SHA512` | BL-564 |
| 14 | `diffie-hellman-group14-sha256` | 4 | 9 | libssh2 | `FiniteFieldDiffieHellman` (BL-739), `SHA256` | BL-564 |
| 15 | `diffie-hellman-group14-sha1` | 5 | 10 | libssh2 | `FiniteFieldDiffieHellman` (BL-739), `SHA1` | BL-564 |
| 16 | `diffie-hellman-group1-sha1` | 6 | 11 | libssh2 | `FiniteFieldDiffieHellman` (BL-739), `SHA1` | BL-564 |
| 17 | `diffie-hellman-group-exchange-sha1` | 7 | 12 | libssh2 | `FiniteFieldDiffieHellman` (BL-739), `SHA1` | BL-564 |
| 18 | `ext-info-c` (RFC 8308 signal) | 8 | 13 | libssh2 | none | offered by BL-563; `server-sig-algs` read by BL-568 |
| 19 | `kex-strict-c-v00@openssh.com` (strict KEX) | 9 | 14 | libssh2 | none | offered and enforced (no other message before the first `NEWKEYS`) by BL-563; sequence numbers reset at each `NEWKEYS` by BL-564 |

The group-exchange sizes (minimum, preferred, maximum bits) are libssh2 1.11.1's; BL-564
measures them from the reference build's `SSH_MSG_KEX_DH_GEX_REQUEST` before pinning.

#### Host key

| # | Name | W | O | From | Primitive | Task |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `ecdsa-sha2-nistp256` | | 1 | libssh2 | `ECDsa` P-256, `SHA256` | BL-564 |
| 2 | `ecdsa-sha2-nistp384` | | 2 | libssh2 | `ECDsa` P-384, `SHA384` | BL-564 |
| 3 | `ecdsa-sha2-nistp521` | | 3 | libssh2 | `ECDsa` P-521, `SHA512` | BL-564 |
| 4 | `ecdsa-sha2-nistp256-cert-v01@openssh.com` | | 4 | libssh2 | as 1, plus the CA's key type | BL-749 |
| 5 | `ecdsa-sha2-nistp384-cert-v01@openssh.com` | | 5 | libssh2 | as 2, plus the CA's key type | BL-749 |
| 6 | `sk-ssh-ed25519-cert-v01@openssh.com` | | | libssh | `Ed25519` (BL-672), `SHA256` | BL-749 |
| 7 | `ecdsa-sha2-nistp521-cert-v01@openssh.com` | | 6 | libssh2 | as 3, plus the CA's key type | BL-749 |
| 8 | `ssh-ed25519` | | 7 | libssh2 | `Ed25519` (BL-672) | BL-678 |
| 9 | `ssh-ed25519-cert-v01@openssh.com` | | 8 | libssh2 | `Ed25519` (BL-672) | BL-749 |
| 10 | `sk-ssh-ed25519@openssh.com` | | | libssh | `Ed25519` (BL-672), `SHA256` | BL-749 |
| 11 | `sk-ecdsa-sha2-nistp256@openssh.com` | | | libssh | `ECDsa` P-256, `SHA256` | BL-749 |
| 12 | `rsa-sha2-512` | 1 | 9 | libssh2 | `RSA` PKCS #1 v1.5, `SHA512` | BL-564 |
| 13 | `rsa-sha2-256` | 2 | 10 | libssh2 | `RSA` PKCS #1 v1.5, `SHA256` | BL-564 |
| 14 | `sk-ecdsa-sha2-nistp256-cert-v01@openssh.com` | | | libssh | `ECDsa` P-256, `SHA256` | BL-749 |
| 15 | `rsa-sha2-512-cert-v01@openssh.com` | 3 | 11 | libssh2 | as 12 | BL-749 |
| 16 | `rsa-sha2-256-cert-v01@openssh.com` | 4 | 12 | libssh2 | as 13 | BL-749 |
| 17 | `ssh-rsa` | 5 | 13 | libssh2 | `RSA` PKCS #1 v1.5, `SHA1` | BL-564 |
| 18 | `ssh-rsa-cert-v01@openssh.com` | 6 | 14 | libssh2 | as 17 | BL-749 |
| 19 | `ssh-dss` | | | libssh2 (`LIBSSH2_DSA_ENABLE`) | `DsaSignature` (BL-745), `SHA1` | BL-564 |

#### Cipher (the same list in both directions)

| # | Name | W | O | From | Primitive | Task |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `chacha20-poly1305@openssh.com` | 1 | 1 | libssh2, libssh | `ChaCha20`, `Poly1305` (BL-673) | BL-679 |
| 2 | `aes256-gcm@openssh.com` | | 2 | libssh2, libssh | `AesGcm` (16-byte tag) | BL-565 |
| 3 | `aes128-gcm@openssh.com` | | 3 | libssh2, libssh | `AesGcm` (16-byte tag) | BL-565 |
| 4 | `aes256-ctr` | 2 | 4 | libssh2, libssh | `AesCtr` (BL-737) | BL-565 |
| 5 | `aes192-ctr` | 3 | 5 | libssh2, libssh | `AesCtr` (BL-737) | BL-565 |
| 6 | `aes128-ctr` | 4 | 6 | libssh2, libssh | `AesCtr` (BL-737) | BL-565 |
| 7 | `aes256-cbc` | 5 | 7 | libssh2, libssh | `Aes` CBC | BL-680 |
| 8 | `rijndael-cbc@lysator.liu.se` | 6 | 8 | libssh2 | `Aes` CBC, 256-bit key (the old name of 7) | BL-680 |
| 9 | `aes192-cbc` | 7 | 9 | libssh2, libssh | `Aes` CBC | BL-680 |
| 10 | `aes128-cbc` | 8 | 10 | libssh2, libssh | `Aes` CBC | BL-680 |
| 11 | `blowfish-cbc` | | 11 | libssh2 | `Blowfish` (BL-674), CBC | BL-680 |
| 12 | `arcfour128` | 9 | 12 | libssh2 | `Rc4` (BL-676), first 1536 bytes discarded (RFC 4345) | BL-680 |
| 13 | `arcfour` | 10 | 13 | libssh2 | `Rc4` (BL-676), no discard | BL-680 |
| 14 | `cast128-cbc` | | 14 | libssh2 | `Cast128` (BL-676), CBC | BL-680 |
| 15 | `3des-cbc` | 11 | 15 | libssh2, libssh | `TripleDES` CBC | BL-680 |

CBC over Blowfish and CAST-128 is chained in `Curl.Protocol.Ssh.UnitLibrary` over the
block operation the primitive exposes; CBC over AES and 3DES uses the BCL's own CBC.
With an AEAD cipher (1 to 3) the MAC list is not consulted, as RFC 5647 and OpenSSH
specify.

#### MAC (the same list in both directions)

| # | Name | W | O | From | Primitive | Task |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | `hmac-sha2-256` | 1 | 1 | libssh2, libssh | `HMACSHA256` | BL-565 |
| 2 | `hmac-sha2-256-etm@openssh.com` | 2 | 2 | libssh2, libssh | `HMACSHA256`, encrypt-then-MAC | BL-565 |
| 3 | `hmac-sha2-512` | 3 | 3 | libssh2, libssh | `HMACSHA512` | BL-565 |
| 4 | `hmac-sha2-512-etm@openssh.com` | 4 | 4 | libssh2, libssh | `HMACSHA512`, encrypt-then-MAC | BL-565 |
| 5 | `hmac-sha1` | 5 | 5 | libssh2, libssh | `HMACSHA1` | BL-680 |
| 6 | `hmac-sha1-etm@openssh.com` | 6 | 6 | libssh2, libssh | `HMACSHA1`, encrypt-then-MAC | BL-750 |
| 7 | `hmac-sha1-96` | 7 | 7 | libssh2 | `HMACSHA1`, first 12 bytes | BL-680 |
| 8 | `hmac-md5` | 8 | 8 | libssh2, libssh | `HMACMD5` | BL-680 |
| 9 | `hmac-md5-96` | 9 | 9 | libssh2 | `HMACMD5`, first 12 bytes | BL-680 |
| 10 | `hmac-ripemd160` | | 10 | libssh2 | `HmacRipemd160` (BL-675) | BL-680 |
| 11 | `hmac-ripemd160@openssh.com` | | 11 | libssh2 | `HmacRipemd160` (BL-675) | BL-680 |
| 12 | `hmac-md5-etm@openssh.com` | | | libssh | `HMACMD5`, encrypt-then-MAC | BL-750 |

#### Compression (the same list in both directions)

| # | Name | Offered | Primitive | Task |
| --- | --- | --- | --- | --- |
| 1 | `zlib` | with `--compressed-ssh`, every preset | `ZLibStream`, one stream per direction for the session, from the first `NEWKEYS` | BL-575 |
| 2 | `zlib@openssh.com` | with `--compressed-ssh`, every preset | as 1, starting after `SSH_MSG_USERAUTH_SUCCESS` | BL-575 |
| 3 | `none` | always | none | BL-563 |

### Primitives

Every primitive above comes from the BCL only when ADR-0118 lists it under "Taken from
the BCL"; everything else is `Curl.Cryptography.UnitLibrary`'s. A primitive the BCL
lacks on any CI platform is hand-built there, never taken from a package and never a
reason to block or leave out an algorithm. This decision finds one ADR-0118 did not
list, **sntrup761** (Streamlined NTRU Prime, for `sntrup761x25519-sha512`), filed as
BL-747 and added to ADR-0118's table. The session keys are derived with the negotiated
exchange hash (RFC 4253 section 7.2, extended by re-hashing when a key is longer than
one digest). Random bytes (the `KEXINIT` cookie, packet padding, the ephemeral
key-exchange keys) come from an injected `ISshRandomSource` whose production
implementation calls `RandomNumberGenerator.Fill`.

### Private and public key files

`--key` is read in every format either SSH backend reads, on every platform, for RSA,
DSA, ECDSA (P-256, P-384, P-521) and Ed25519 keys, by one decoder built on
`System.Formats.Asn1` (no platform key import, so DSA and Ed25519 load on macOS too):

| Format | Armor | Encryption read (with `--pass`) |
| --- | --- | --- |
| PKCS #1 RSA, OpenSSL DSA, SEC1 EC | `RSA PRIVATE KEY`, `DSA PRIVATE KEY`, `EC PRIVATE KEY` | none, or legacy PEM encryption (`Proc-Type: 4,ENCRYPTED`, `DEK-Info:` `DES-CBC`, `DES-EDE3-CBC`, `AES-128-CBC`, `AES-192-CBC`, `AES-256-CBC`; key from OpenSSL's `EVP_BytesToKey` with `MD5`, one iteration) using `DES`, `TripleDES`, `Aes` |
| PKCS #8 (RSA, DSA, EC, Ed25519 OID 1.3.101.112) | `PRIVATE KEY` | none |
| Encrypted PKCS #8 | `ENCRYPTED PRIVATE KEY` | PBES2 (PBKDF2 with HMAC-SHA1/-SHA256/-SHA384/-SHA512 via `Rfc2898DeriveBytes.Pbkdf2`; AES-128/192/256-CBC, DES-EDE3-CBC, DES-CBC) and PBES1 (`pbeWithMD5AndDES-CBC`, `pbeWithSHA1AndDES-CBC`), parsed by hand because `System.Security.Cryptography.Pkcs` is a package |
| `openssh-key-v1` (RSA, DSA, ECDSA, Ed25519) | `OPENSSH PRIVATE KEY` | KDF `none`, or `bcrypt` (`BcryptPbkdf`, BL-674) with cipher `aes256-ctr` (OpenSSH's default), `aes128-ctr`, `aes192-ctr`, `aes128-cbc`, `aes192-cbc`, `aes256-cbc`, `aes128-gcm@openssh.com`, `aes256-gcm@openssh.com`, `chacha20-poly1305@openssh.com` or `3des-cbc`, from the same cipher classes as the packet layer; the two check integers must match, else the passphrase is wrong |

PuTTY `.ppk` files, and `sk-` security-key private keys (which need a FIDO
authenticator curl never drives), are read by neither backend as curl uses them; they
fail with the exit code and message curl gives, measured by BL-568 and BL-681. The key
algorithm offered for a user key follows the server's `server-sig-algs` (RFC 8308):
`rsa-sha2-512`, then `rsa-sha2-256`, then `ssh-rsa` for an RSA key. `--pubkey` reads an
OpenSSH one-line public key (`<type> <base64> [comment]`); without it the public key is
derived from the private key, as libssh2 1.11.1 does. `--key-type` does not apply to SSH.

### Transfer options

`ITransferContext` gains one record, `SshOptions? Ssh` (null outside `scp` and `sftp`,
as `Http` is null outside HTTP), with `PrivateKeyPath` (`--key`), `PublicKeyPath`
(`--pubkey`), `PrivateKeyPassphrase` (`--pass`), `KnownHostsPath` (the file to check,
null when `-k` turns the check off), `HostPublicKeyMd5` (`--hostpubmd5`, 32 hex digits),
`HostPublicKeySha256` (`--hostpubsha256`, base64) and `Compression`
(`--compressed-ssh`). The console, not the handler, resolves the default
`~/.ssh/known_hosts` and fails with `curl: Could not find a known_hosts file` and exit 2
when it is missing and `-k` was not given. The existing `Credentials`, `QuoteCommands`,
`Upload`, `Range`, `ResumeFrom`, `CreateFileMode`, `FtpCreateDirectories` and `ListOnly`
members carry the rest. Timeouts follow ADR-0117: the connector owns the connect
timeout, the runner owns `-m`, the handler honours the cancellation token and reports
progress.

### Structure

Namespace `Curl.Protocol.Ssh` with one sub-namespace per folder. The handler takes an
`IConnector` (ADR-0005) and never a socket.

| Folder | Types | Role |
| --- | --- | --- |
| (root) | `SshProtocolHandler`, `ISshRandomSource`, `SystemSshRandomSource` | `IProtocolHandler` for `scp` and `sftp`: connects, runs the transport, authenticates, then hands the channel to SFTP or SCP; maps every failure to a `CurlExitCode` |
| `Transport` | `SshIdentificationExchange`, `SshPacketReader`, `SshPacketWriter`, `SshTransport`, `SshWireReader`, `SshWireWriter`, `SshMessageNumber` | RFC 4253: identification strings, binary packets with sequence numbers, RFC 4251 data types, server-initiated re-exchange, `DISCONNECT`, `IGNORE`, `DEBUG`, `UNIMPLEMENTED` |
| `Negotiation` | `SshAlgorithmPreferences`, `SshAlgorithmNegotiator`, `SshNegotiatedAlgorithms`, `SshAlgorithmCatalogue` | the three presets, known-hosts narrowing, RFC 4253 section 7.1 selection, and the one table mapping each name to its implementation |
| `KeyExchange` | `ISshKeyExchange`, `EcdhSshKeyExchange`, `Curve25519SshKeyExchange`, `FiniteFieldSshKeyExchange`, `GroupExchangeSshKeyExchange`, `HybridKemSshKeyExchange`, `SshKeyDerivation` | shared secret, exchange hash, session identifier, the six keys |
| `HostKeys` | `ISshSignatureVerifier`, `RsaSshSignatureVerifier`, `EcdsaSshSignatureVerifier`, `Ed25519SshSignatureVerifier`, `DsaSshSignatureVerifier`, `SecurityKeySshSignatureVerifier`, `OpenSshCertificate`, `KnownHostsFile`, `SshHostKeyChecker` | host-key signature over the exchange hash, `--hostpubmd5`, `--hostpubsha256`, known hosts with hashed `\|1\|`, `[host]:port`, `@revoked` and `@cert-authority` |
| `PacketProtection` | `ISshPacketProtection`, `CipherAndMacPacketProtection`, `AesGcmPacketProtection`, `ChaCha20Poly1305PacketProtection`, `ISshCipher`, `AesCtrSshCipher`, `CbcSshCipher`, `Rc4SshCipher`, `SshMac` | encrypts, decrypts and authenticates each packet after `NEWKEYS` |
| `Compression` | `ISshCompression`, `NoSshCompression`, `ZlibSshCompression` | per-direction streams, immediate or delayed |
| `Authentication` | `SshUserAuthenticator`, `SshPasswordAuthentication`, `SshKeyboardInteractiveAuthentication`, `SshPublicKeyAuthentication` | RFC 4252 and RFC 4256 in curl's method order, `USERAUTH_BANNER` |
| `Keys` | `SshPrivateKeyReader`, `PemPrivateKeyDecoder`, `Pkcs8PrivateKeyDecoder`, `OpenSshPrivateKeyDecoder`, `SshPublicKeyFile`, `SshPrivateKey` and one subclass per key type | the key-file formats above and signing for public-key authentication |
| `Connection` | `SshConnectionLayer`, `SshSessionChannel` | RFC 4254: channel open, window adjust, `exec`, `subsystem`, EOF, close, `exit-status` |
| `Sftp` | `SftpClient`, `SftpPacket`, `SftpAttributes`, `SftpStatusMapper`, `SftpQuoteCommandRunner`, `SftpDirectoryListingWriter` | draft-ietf-secsh-filexfer-02 version 3: download, upload, listing, ranges, resume, `-Q` commands |
| `Scp` | `ScpDownload`, `ScpUpload` | `scp -f` and `scp -t` over an `exec` channel |

### Tests

`Curl.Protocol.Ssh.UnitTests` drives the handler with no network through an in-memory
SSH peer, `InMemorySshServer`, built from the library's own transport, key-exchange,
packet-protection and connection classes in the server role and from the same
`Curl.Cryptography` and BCL primitives, over an in-memory duplex `IConnection`. The peer
uses fixed host and user keys checked in as test resources (generated once with
`ssh-keygen`; Ed25519 keys from RFC 8032 section 7.1), and both ends take a
deterministic `ISshRandomSource`, so every session's bytes are reproducible. The peer is
scripted per test to offer chosen algorithms, refuse, corrupt a MAC, send a bad
signature, or disconnect, which is how each failure's exit code is pinned. The
client's `KEXINIT` is compared with the name-lists measured above (cookie and padding
excepted), and every message and exit code is measured from the reference build with
`Record-CurlExchange.ps1` (with `-NoServer` against a real `sshd` where a full session is
needed) before a test pins it. Tests are platform-neutral: the preset is a constructor
argument, so the Windows and OpenSSL presets are both tested on every platform.

## Consequences

What each SSH task relies on from this decision:

- **BL-561:** the `SshOptions` record and its seven members, on `ITransferContext.Ssh`.
- **BL-562:** the five options parse into what `SshOptions` needs; `--key-type` does not
  apply to SSH.
- **BL-563:** the identification string, the three presets and the full-set order, the
  known-hosts narrowing and its exit 79, `ext-info-c` and strict KEX offered, and exit 2
  with `Failure establishing ssh session: -1, Unable to exchange encryption keys` when
  the exchange fails.
- **BL-564:** the ECDH, finite-field and group-exchange rows and the RSA, ECDSA and DSA
  host-key rows, from the BCL, `FiniteFieldDiffieHellman` (BL-739) and `DsaSignature`
  (BL-745); it now depends on BL-739 and BL-745. Strict-KEX sequence reset. It measures
  the group-exchange sizes.
- **BL-565:** the AES-GCM, AES-CTR (BL-737) and SHA-2 MAC rows, and the packet
  protection seam the other cipher tasks extend.
- **BL-566:** known hosts, `--hostpubmd5`, `--hostpubsha256`, `-k` as a null
  `KnownHostsPath`, and the narrowing it feeds to BL-563.
- **BL-567:** the `Authentication` folder, password and keyboard-interactive.
- **BL-568:** every key-file format in the table except `openssh-key-v1` Ed25519 and
  bcrypt, `server-sig-algs` choosing the RSA signature algorithm, and the public key
  from `--pubkey` or derived.
- **BL-569 to BL-573:** the `Connection` and `Sftp` folders.
- **BL-574 and BL-577:** the `Scp` folder.
- **BL-575:** the compression rows: offered as `zlib,zlib@openssh.com,none` (measured
  order, which corrects the order its goal names), `ZLibStream`, delayed start for the
  `@openssh.com` form.
- **BL-576:** the composition injects `WindowsReference` on Windows and
  `OpenSslReference` elsewhere, and the console checks for the known-hosts file.
- **BL-578:** the `-v` lines measured here (`SSH: libssh2 cryptography backend: WinCNG`
  on Windows, `openssl compatible` elsewhere, `SSH: user '<name>'`, the failure line).
- **BL-678:** `curve25519-sha256`, its `@libssh.org` alias and `ssh-ed25519`, at the
  positions above (OpenSSL preset only by default).
- **BL-679:** `chacha20-poly1305@openssh.com`, first in both presets.
- **BL-680:** the CBC, 3DES, Blowfish, CAST-128, arcfour and SHA-1, MD5 and RIPEMD-160
  MAC rows it names.
- **BL-681:** `openssh-key-v1` with bcrypt and its ciphers, and Ed25519 keys in every
  format.
- **Filed by this decision:** BL-747 (sntrup761 primitive), BL-748 (the post-quantum
  hybrid exchanges), BL-749 (host-key certificates and `sk-` host keys), BL-750
  (`hmac-sha1-etm@openssh.com`, `hmac-md5-etm@openssh.com`).

Also:

- Curl's `KEXINIT` is indistinguishable from the platform curl's, so a server that
  accepts one accepts the other and one that refuses one refuses the other. On Windows
  that includes the reference build's limits: no ECDSA, Ed25519 or curve25519 host keys
  or exchanges by default, and exit 79 for an Ed25519 known-hosts entry, although Curl
  implements them. Matching the platform build is the rule; `Full` is one constructor
  argument away if that rule ever changes.
- Algorithms only `Full` offers (the post-quantum exchanges, the `sk-` host keys,
  `ssh-dss`, `hmac-md5-etm@openssh.com`) run only in tests until something selects that
  preset. They are built because a complete reimplementation leaves nothing out, and
  their tasks are Normal priority behind the default-set work.
- Several offered algorithms are weak (`diffie-hellman-group1-sha1`, `ssh-rsa`,
  `arcfour`, `3des-cbc`, `hmac-md5`); the reference builds offer them, late in the list,
  so Curl does too, and the server's preference decides.
- Everything is one implementation on every platform, so a Windows lane's test result
  holds on macOS; the price is ~50 types in one library, each kept under the complexity
  gate by its narrow role.

## Alternatives considered

- **Offer the full set on every platform.** Better reach on Windows, but the `KEXINIT`
  and the Windows exit-79 case would differ from the platform curl's. Rejected: match the
  platform build.
- **Offer only the OpenSSL reference set everywhere.** One list, but still not the
  Windows build's. Rejected for the same reason.
- **Match libssh (Ubuntu's and Fedora's curl) on Linux.** Neither is the reference
  OpenSSL build of 8.21.0; libssh's extra algorithms are in the full set instead.
- **Build only the default sets.** Leaves out algorithms curl's backends offer.
  Rejected by the standing rule that nothing is left out.
- **Use the platform's key import (`RSA.ImportFromPem`, `ImportEncryptedPkcs8PrivateKey`).**
  Covers RSA and EC on every platform but not Ed25519, not `openssh-key-v1`, and not DSA
  on macOS, so a second decoder would exist anyway. Rejected for one decoder.
- **A package (SSH.NET, BouncyCastle).** Forbidden by the BCL-only rule and a native AOT
  risk. Rejected.
