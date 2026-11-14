# ADR-1267: The explicit-credential LDAP NTLM bind seals the session

- Status: Accepted
- Date: 2026-11-13
- Task: BL-5142 (finding STD-049, split from BL-5094)
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0's Schannel build binds `ldap://` with `-u` and `--ntlm` or `--negotiate` through
WinLDAP's Sicily NTLM bind, offering NEGOTIATE_SIGN and NEGOTIATE_SEAL (Type-1 flags
`0xe20882b7`), and seals every later LDAP message in the SASL security layer: a 4-byte
big-endian length, then the wrapped buffer. Against a server that cannot unseal (BL-5094's
BER mock) curl ends with `curl: (39) LDAP remote: Unavailable`. Curl's port offered
`0xa208b207` and sent everything after the bind in clear.

## Decision

- The negotiate message carries `0xe20882b7`. The challenge is answered with a key exchange
  (`NtlmChallengeAnswerer.AnswerWithKeyExchange`, BL-5141) and, once the response bind
  succeeds, `LdapExchange.StartSecurityLayer` gets an `NtlmLdapLogonAuthentication` whose
  wrap is the 16-byte NTLM signature then the sealed bytes, the layout the current-user path
  already sends.
- The layer starts only when the negotiated flags hold SEAL, extended session security and
  128-bit keys, the keys `NtlmSessionSecurity` derives; a server that grants none of them
  (an NTLMv1 challenge) keeps the session unsealed, as SSPI cannot seal it either.
- A search whose sealed reply cannot be read (closed, too long, not unsealed) after this bind
  ends `(39) LDAP remote: Unavailable`, as measured in BL-5094; after the current-user bind it
  stays `Server Down`, as measured there. `LdapExchange.WinLdapLostResultCode` carries which.

## Consequences

The password-derived bind no longer leaves the search and its results in clear. The
current-user path's bytes are unchanged.
