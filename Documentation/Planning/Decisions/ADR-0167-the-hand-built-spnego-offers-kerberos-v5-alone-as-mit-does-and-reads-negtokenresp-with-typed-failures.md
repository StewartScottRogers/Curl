# ADR-0167 — The hand-built SPNEGO offers Kerberos V5 alone, as MIT's library does for curl, and reads NegTokenResp with typed failures

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-692.

## Context

ADR-0142 routes Negotiate off Windows to the system GSS-API library and, only when that
answers `Unsupported`, to hand-built SPNEGO around hand-built Kerberos (S+K). It left the
mechanism list to BL-692's measurement and guessed "Kerberos V5, then the legacy Microsoft
OID". On Windows Negotiate is always SSPI's, so the hand-built SPNEGO has only the Linux
and macOS curl to match.

### Measured on 2026-09-28

- **Windows**, curl 8.21.0 (Schannel, SSPI), `--negotiate` with `-u :`, `-u u:p`,
  `-u EXAMPLE\u:p` and `-u u@EXAMPLE.COM:p` against a loopback `401 Negotiate` server
  through `Record-CurlExchange.ps1`: no `Authorization` header, one request, exit 0. On a
  machine outside a domain SSPI's Negotiate package yields nothing curl will send.
- **Ubuntu**, curl 8.18.0 (OpenSSL, mit-krb5 1.22.1, no `gss-ntlmssp`), `--negotiate -u :`
  against a loopback `401 Negotiate` server, with `KRB5CCNAME` naming a hand-written
  `FILE:` cache that holds a service ticket for `HTTP/localhost@TEST.ORG` (so MIT builds an
  AP-REQ without a KDC): curl sent a 427-byte token whose NegTokenInit holds
  `mechTypes` = { `1.2.840.113554.1.2.2` } and a `mechToken`, with no `reqFlags` and no
  `mechListMIC`. MIT offers only the mechanisms it holds credentials for, so the legacy
  Microsoft OID and NTLMSSP do not appear.

## Decision

- `SpnegoInitialToken.Encode(mechanismTypes, mechanismToken)` writes the RFC 2743
  framing (`[APPLICATION 0]`, `thisMech` `1.3.6.1.5.5.2`) around a NegTokenInit holding
  `mechTypes` and `mechToken` only, in DER, with `System.Formats.Asn1`. It takes the list
  as a parameter, so any list can be offered and pinned.
- The hand-built route offers `SpnegoMechanism.MitKerberosMechanismTypes`: Kerberos V5
  alone, as measured. Re-encoding the measured AP-REQ with that list reproduces curl's
  427 bytes exactly, and a test pins it.
- `SpnegoNegotiationResponse.Decode` reads a NegTokenResp as acceptors send it (the bare
  `[1]` choice, no framing) in BER, with every field optional. Anything that is not the
  `[1]` choice is `SpnegoTokenError.UnexpectedToken`; truncation, trailing bytes, fields
  out of order or of the wrong type, an unknown field and a `negState` outside RFC 4178's
  four values are `SpnegoTokenError.Malformed`, thrown as `SpnegoTokenException`.
  `Encode` writes the same structure, for the initiator's later tokens.

## Consequences

- The hand-built Negotiate token matches the platform curl's byte for byte wherever the
  AP-REQ inside it does.
- NTLM through SPNEGO is not offered by the hand-built route, because MIT's curl without
  `gss-ntlmssp` does not offer it; the encoder can still carry it if that changes.

## Alternatives considered

- **Kerberos V5, the legacy Microsoft OID and NTLMSSP (SSPI's order).** Rejected: no
  Linux or macOS curl sends it, and on Windows SSPI writes the token itself.
- **Accept the framed initial-token form for NegTokenResp too.** Rejected: RFC 4178
  sends every token after the first unframed, and a framed answer is a server defect this
  decoder reports as `UnexpectedToken`.
