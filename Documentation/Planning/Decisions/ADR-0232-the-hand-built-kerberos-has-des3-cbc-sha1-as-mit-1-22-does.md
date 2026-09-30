# ADR-0232 — The hand-built Kerberos has des3-cbc-sha1 as MIT 1.22 does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-894.
Extends ADR-0161 (the library's encryption types) and ADR-0209 (which dropped `des3` from
the offered list because the library could not encrypt with it).

## Context

MIT 1.22 still has `des3-cbc-sha1` (16, RFC 3961 section 6.3), and `permitted_enctypes =
des3 DEFAULT` makes `kinit` offer `16 18 17 20 19 25 26` (ADR-0209's measurement). The
BCL has triple DES (`TripleDES`), HMAC-SHA-1 and SHA-1, so nothing needs hand-building
beyond RFC 3961's key production. Three points had a choice:

1. RFC 3961 section 6.3.1 says random-to-key corrects DES weak and semi-weak keys; MIT 1.22's
   DES3 random-to-key only fixes parity, and a random key is weak with probability about 2^-52.
2. The plaintext is padded with zeros to a whole 8-byte block, and decryption cannot tell
   padding from plaintext. MIT's `krb5_c_decrypt` returns it padded, and
   `k5_asn1_full_decode` ignores bytes after the decoded value for this reason; this
   library's decoders refused trailing bytes.
3. A ciphertext whose encrypted part is not whole blocks: MIT fails with
   `KRB5_BAD_MSIZE`.

## Decision

- **`Des3CbcSha1KerberosEncryption`**, given by `KerberosEncryption.Create` for
  `KerberosEncryptionType.Des3CbcSha1` (16): 24-byte keys, checksum type 12
  (`hmac-sha1-des3-kd`, the full 20-byte HMAC), an 8-byte confounder, CBC with a zero IV,
  `DR`/`DK` with n-fold to one block and 21 random bytes, string-to-key
  `DK(random-to-key(168-fold(password | salt)), "kerberos")` refusing any parameters, and
  the simplified profile's PRF (SHA-1 cut to two blocks, encrypted under `DK(key, "prf")`).
- **Random-to-key fixes parity only, as MIT does**, not RFC 3961's weak-key correction:
  matching the platform's Kerberos is the standing rule, and the difference is
  unobservable in practice.
- **Decryption gives the padded plaintext, as MIT's does**, and the two places that decode
  a decrypted value — `KerberosKdcClient` (the AS-REP and TGS-REP encrypted parts) and
  `KerberosGssContext` (the AP-REP encrypted part) — first drop the bytes after the value
  with `KerberosAsn1.WithoutPadding`, as MIT's decoder ignores them. The decoders
  themselves stay strict for every other message.
- **A ciphertext not whole blocks fails as the new
  `KerberosCryptographyError.CiphertextNotWholeBlocks`**, MIT's `KRB5_BAD_MSIZE`.
- `KerberosKdcClient` now offers and serves 16 wherever `krb5.conf` names it, since its
  offerable types are those `KerberosEncryptionType` names.

## Consequences

- RFC 3961 appendix A.3 and A.4 and MIT's `t_decrypt.c` and `t_cksums.c` vectors pin it.
- A GSS-API context whose key is des3 still gets RFC 4121 tokens; MIT makes RFC 1964-style
  DES3 tokens for it. BL-962 builds those.
