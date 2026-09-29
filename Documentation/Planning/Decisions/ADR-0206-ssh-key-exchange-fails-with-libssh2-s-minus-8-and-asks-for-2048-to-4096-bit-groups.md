# ADR-0206 — The SSH key exchange fails with libssh2's -8, asks for 2048-to-4096-bit groups, and answers a server's re-exchange

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-564.
Builds on ADR-0122, which chose the algorithms; this records what ADR-0122 left open.

## Context

BL-564 runs the key exchange `SshTransport` agrees (ADR-0122's ECDH, finite-field and
group-exchange rows), checks the RSA, ECDSA and DSA host-key signatures, derives the
keys and exchanges `NEWKEYS`. Four questions were open: what curl prints when the
exchange fails after the algorithms are agreed, which group sizes the group exchange asks
for and accepts, what the transport does when the server starts a second exchange, and
how the tests fix the ephemeral keys.

Measured 2026-09-29 against the Windows reference build (`curl 8.21.0 ... libssh2/1.11.1`,
WinCNG), with `Record-CurlExchange.ps1 -Response <bytes> -HoldOpenMilliseconds 3000` and
`-k -u u:p sftp://127.0.0.1:<port>/x`: a server that sends its identification and a
`KEXINIT` offering `diffie-hellman-group14-sha256`, `rsa-sha2-256`, `aes128-ctr`,
`hmac-sha2-256` and `none`, then one of:

| Server's next packet | Exit | stderr |
| --- | --- | --- |
| `KEXDH_REPLY` with an all-zero RSA signature | 2 | `curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys` |
| `KEXDH_REPLY` with f = 1 | 2 | same |
| `KEXDH_REPLY` with f = 0 | 2 | same |
| `KEXDH_REPLY` whose signature blob names `rsa-sha2-512` | 2 | same |
| nothing, then close | 2 | same |

With the server offering `diffie-hellman-group-exchange-sha256` or `-sha1`, curl's
`SSH_MSG_KEX_DH_GEX_REQUEST` asked for minimum 2048, preferred 4096 and maximum 4096 bits.
The OpenSSL build could not be measured the same day (the Docker engine was down).

## Decision

- **Every failure after the algorithms are agreed is exit 2, `-8, Unable to exchange
  encryption keys`** (`LIBSSH2_ERROR_KEY_EXCHANGE_FAILURE`): the peer closing, an
  unexpected message, a malformed message or negative `mpint`, a public value outside its
  group (f not in 1 < f < p - 1, or a point not on its curve), a group-exchange group
  outside the sizes below, a host key or signature blob naming another algorithm, a key
  the platform refuses, a signature that does not verify, and a missing `NEWKEYS`. The
  measured cases show libssh2 folds all of these into one code; the unmeasured ones take
  the same code because they fail in the same libssh2 function.
- **Group exchange asks for (2048, 4096, 4096) and accepts a prime of 2048 to 4096
  bits**, on every preset, as the Windows build asks. A prime outside the range it asked
  for is refused rather than used: RFC 4419 lets the client refuse it, and a server that
  honours the request never sends one. BL-888 measures the OpenSSL build's request and
  splits the sizes by preset if they differ.
- **A NIST point is checked by hand** (`SshNistCurve.DecodePublicPoint`: uncompressed,
  coordinates below p, y^2 = x^3 - 3x + b) before the BCL sees it, so every platform
  refuses the same points instead of leaving it to each platform's key import.
- **A server's later `KEXINIT` is answered** (`SshTransport.ReExchangeKeysAsync`), as
  ADR-0122 gives the transport the "server-initiated re-exchange" and libssh2 does: the
  client sends its own `KEXINIT`, the algorithms are agreed again, and a new exchange runs
  that keeps the first exchange's hash as the session identifier. `IGNORE`, `DEBUG` and
  `UNIMPLEMENTED` are skipped during it even under strict key exchange, which binds only
  the first exchange; sequence numbers restart at each `NEWKEYS` once strict key exchange
  was agreed. Its failures take the codes above (no shared algorithm stays `-5`); the
  handler tasks re-map them if curl prints something else mid-transfer.
- **Ephemeral keys come from `ISshEphemeralKeySource`**, not `ISshRandomSource`: the BCL's
  `ECDiffieHellman` draws its own private key, so a byte source cannot fix it. The
  production source is `ECDiffieHellman.Create(curve)` and
  `FiniteFieldDiffieHellman.Generate(group)`; tests inject fixed keys and pin the
  exchange hash and the six keys.
- **`NEWKEYS` hands the keys out; it does not install them.** `ExchangeKeysAsync` returns
  an `SshKeyDerivation` that derives each of the six keys (RFC 4253 section 7.2, extended
  by re-hashing) at the length its cipher or MAC needs; BL-565's packet protection takes
  them from there.

## Consequences

- One message for every key-exchange failure keeps the handler's mapping to one line
  and matches every case measured.
- The client refuses a server's 8192-bit group on every platform. If BL-888 finds the
  OpenSSL build asks for 8192, the Linux and macOS presets widen.
- `Curl.Protocol.Ssh.UnitLibrary` now references `Curl.Cryptography.UnitLibrary`, as
  ADR-0120 allows, for `FiniteFieldDiffieHellman` and `DsaSignature`.

## Alternatives considered

- **Leave point validation to the BCL.** Windows and OpenSSL validate on import, but
  that is each platform's behaviour rather than the library's; rejected for one check on
  every platform.
- **Refuse a server's re-exchange.** Simpler, but OpenSSH re-keys long transfers
  (`RekeyLimit`), and libssh2 answers; rejected.
- **Accept any prime the server sends.** libssh2 may; but a group smaller than asked for
  weakens the session and one larger than asked for is a server bug. Rejected.
