# ADR-0209 — The hand-built Kerberos offers the encryption types krb5.conf names, resolved as MIT does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-828.
Amends ADR-0168, which offered one fixed list in every request.

## Context

ADR-0168 offered `aes256-cts-hmac-sha1-96`, `aes128-cts-hmac-sha1-96`,
`aes256-cts-hmac-sha384-192`, `aes128-cts-hmac-sha256-128` and `rc4-hmac` in every AS-REQ
and TGS-REQ, whatever `krb5.conf` said. ADR-0160 left `permitted_enctypes` and
`default_tkt_enctypes` as unresolved words. MIT reads the three relations with
`krb5int_parse_enctype_list` (`src/lib/krb5/krb/init_ctx.c`) and the names of
`src/lib/crypto/krb/etypes.c`.

MIT 1.22.1 (Ubuntu's `krb5-user` 1.22.1-2ubuntu4.1 under WSL) was measured: `kinit alice`
against an unanswering KDC with each relation below as `permitted_enctypes`, the etype
list read from the AS-REQ datagram `strace` recorded.

| Relation | Offered |
| --- | --- |
| (unset) | 18 17 20 19 25 26 |
| `DEFAULT -aes` | 25 26 |
| `des3 DEFAULT` | 16 18 17 20 19 25 26 |
| `DEFAULT +rc4 -camellia` | 18 17 20 19 23 |
| `camellia aes` | 26 25 18 17 20 19 |
| `rc4 des3` | 23 16 |
| `AES256-CTS,Default,-AES128-CTS` | 18 20 19 25 26 |
| `des-cbc-crc bogus aes128-cts`, `des des-cbc-md5 aes128-cts` | 17 |
| `+aes -aes256-cts aes256-cts` | 17 20 19 18 |
| `arcfour-hmac-exp aes128-cts` | 17; with `allow_weak_crypto = true`, 24 17 |
| `bogus`, `17 18` | nothing: "No supported encryption types (config file error?)" |
| `aes256-cts` with `default_tkt_enctypes = DEFAULT` | 18 17 20 19 25 26 |
| `aes128-cts` with `default_tkt_enctypes = aes256-cts` | 18 |

## Decision

- **`KerberosEncryptionTypeList.Resolve`** resolves a relation's words as MIT does: each
  word without regard to case is `DEFAULT` (MIT 1.22's default list, 18 17 20 19 25 26),
  a family (`aes` 18 17 20 19, `camellia` 26 25, `rc4` 23, `des3` 16) or a name or alias
  from MIT's table; `-` removes its types, `+` or no sign appends those not yet listed, an
  unknown word (single DES, numbers, `des3-cbc-raw`) is skipped. `arcfour-hmac-exp` (24),
  MIT's one weak type, is listed only when `allow_weak_crypto` is set.
- **AS-REQs offer `default_tkt_enctypes`, TGS-REQs `default_tgs_enctypes`**, each
  defaulting to `permitted_enctypes`, which defaults to `DEFAULT`. As measured, the ticket
  list is not intersected with `permitted_enctypes`.
- **Only types this library has are offered.** The resolved list keeps the numbers
  `KerberosEncryptionType` names (17, 18, 19, 20, 23). So the default offer is 18 17 20 19,
  not MIT's 18 17 20 19 25 26, until Camellia (25, 26) is built; `rc4-hmac`, gone from
  MIT's default list, is no longer offered unless named, and `des3-cbc-sha1` (16) waits
  for its own task. Offering a type the client cannot decrypt would let the KDC pick it.
- **A list left empty fails before sending**: `KerberosKdcException` with
  `EncryptionTypeNotSupported`, as MIT fails with `KRB5_CONFIG_ETYPE_NOSUPP`.
- An AS-REP, and the pre-authentication key picked from `PA-ETYPE-INFO2`, must be in the
  AS list; a ticket-granting ticket's session key may be any type the library has.

## Consequences

- `KerberosKdcClient.RequestedEncryptionTypes` (static) is replaced by the instance
  properties `AsRequestEncryptionTypes` and `TgsRequestEncryptionTypes`.
- `KerberosConfiguration` gains `DefaultTicketGrantingServiceEncryptionTypes` and
  `AllowWeakCrypto`.
- The request bytes match MIT's once the Camellia and triple-DES encryption types are
  built (follow-up tasks from BL-828).
