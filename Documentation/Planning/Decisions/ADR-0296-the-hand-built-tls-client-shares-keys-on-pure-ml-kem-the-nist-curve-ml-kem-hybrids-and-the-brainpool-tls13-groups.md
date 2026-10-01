# ADR-0296 — The hand-built TLS client shares keys on pure ML-KEM, the NIST-curve ML-KEM hybrids and the brainpool `tls13` groups

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1049.
Amends ADR-0284 decision 3 for groups: no OpenSSL group name is dropped any more.

## Context

ADR-0284 dropped the `--curves` names `Curl.Tls` had no key share for - `SecP256r1MLKEM768`
(0x11eb), `SecP384r1MLKEM1024` (0x11ed), `MLKEM512`/`MLKEM768`/`MLKEM1024` (0x0200-0x0202)
and `brainpoolP256r1tls13`/`brainpoolP384r1tls13`/`brainpoolP512r1tls13` (0x001f-0x0021) -
so a list of only these failed with exit 35 "no suitable groups" where OpenSSL's curl
connects. `Curl.Cryptography` already had hand-built ML-KEM (all three parameter sets) and
brainpool ECDH.

Measured 2026-10-01 with `Record-CurlExchange.ps1` (`-Curl wsl.exe`, `-ListenAddress`) against
Ubuntu's curl 8.18.0 with OpenSSL 3.5.5: each name alone offers that one group with one key
share, of 1249 (P-256 point and ML-KEM-768 key), 1665, 800, 1184, 1568, 65, 97 and 129 bytes;
lists follow ADR-0284's rules (first group shared unless one is starred).

## Decision

1. **Every name gets its key share.** `TlsNamedGroup.CanShare` accepts the eight groups;
   `OpenSslGroupList` offers them, and `SystemTlsRandomSource` makes their shares.
2. **Pure ML-KEM (draft-ietf-tls-mlkem) is `MlKemKeyShare`:** the encapsulation key out, the
   ciphertext in, the 32-byte ML-KEM secret. A ciphertext of the wrong length gives no secret;
   one of the right length always decapsulates (FIPS 203's implicit rejection), so a forged
   one fails the handshake at the server's Finished.
3. **The NIST-curve hybrids (draft-ietf-tls-ecdhe-mlkem) are `EcdhMlKemKeyShare`, curve
   first** - unlike X25519MLKEM768, whose ML-KEM half leads: the uncompressed point then the
   encapsulation key out, the server's point then the ciphertext in, the ECDH secret (the X
   coordinate) then the ML-KEM secret. It composes an `EcdhKeyShare` (whose point check
   applies) and an ML-KEM key.
4. **The brainpool `tls13` groups (RFC 8734) are `BrainpoolKeyShare`** on the same three
   curves as TLS 1.2's brainpool groups; they stay TLS 1.3 only (`IsTls12EcdheGroup` is
   unchanged).

## Consequences

- `KeyShareKnownAnswerTests` pin each share against OpenSSL 3.5.5's known answers
  (`openssl genpkey` from a fixed ML-KEM seed, `pkeyutl -encap` and `-derive`), and
  `Tls13ClientHandshakeTests` complete a handshake on each group with the in-memory server,
  whose `MlKemServerShare` encapsulates. All eight also completed against OpenSSL 3.5.5's
  `s_server -groups <group>`, which confirms the hybrids' byte order.
- With every known name usable, a starred group without a key share can only be a TLS 1.2
  brainpool group; OpenSSL's curl fails that with exit 35 "no suitable key share" (measured),
  which BL-1082 takes on. The `ec_point_formats` difference ADR-0284 noted now shows on the
  ML-KEM-only lists too; BL-1048 covers it.

## Alternatives considered

- **ML-KEM from the BCL (`System.Security.Cryptography.MLKem`).** Not available on every
  platform Curl runs on, and the hand-built one already serves X25519MLKEM768.
- **One class for all three hybrids.** X25519MLKEM768's halves run in the other order and use
  X25519, so a shared class would carry an order switch; two small classes say what each does.
