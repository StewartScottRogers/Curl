# ADR-0163 — The hand-built Kerberos encodes messages in DER with System.Formats.Asn1 and reads BER with typed failures

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-687.

## Context

The hand-built Kerberos client (ADR-0142's fallback route) must write AS-REQ, TGS-REQ
and AP-REQ, and read AS-REP, TGS-REP, AP-REP and KRB-ERROR, with the structures inside
them (RFC 4120 section 5 and Appendix A). The BCL's `System.Formats.Asn1` reads and
writes BER and DER, but has no GeneralString writer, and RFC 4120 leaves several
encoding details to implementations. The choices were measured against MIT Kerberos
1.22.1: `kinit` and `kvno` against a local `krb5kdc`, recorded with `strace`
(`RecordedKerberosMessages` in `Curl.Kerberos.UnitTests`).

## Decision

- **Write DER, read BER.** Every `Encode` writes DER (`AsnEncodingRules.DER`), as MIT
  and Active Directory do; every `Decode` reads BER, as MIT's decoder accepts. All six
  recorded MIT messages, and the ticket from BL-688's recorded credential cache,
  re-encode to the same bytes.
- **One public type per structure, bytes in and typed values out.** `KerberosKdcRequest`
  (AS-REQ and TGS-REQ, with `KerberosKdcRequestBody`), `KerberosKdcReply` (AS-REP and
  TGS-REP), `KerberosEncryptedKdcReplyPart`, `KerberosApRequest`, `KerberosAuthenticator`,
  `KerberosApReply`, `KerberosEncryptedApReplyPart`, `KerberosErrorMessage`,
  `KerberosTicket`, and the pieces `KerberosPrincipalName`, `KerberosEncryptedData`,
  `KerberosChecksum`, `KerberosPreAuthenticationData` (with `METHOD-DATA`),
  `KerberosEncryptedTimestamp` (`PA-ENC-TS-ENC`), `KerberosEncryptionTypeInfo2Entry`
  (`PA-ETYPE-INFO2`) and `KerberosLastRequest`. `KerberosMessage.PeekType` says which
  message a KDC answered with before either is decoded. Encryption stays in
  `KerberosEncryption` (ADR-0161); these types only carry ciphertext.
- **Every decoding failure is a `KerberosMessageException`**, never an
  `AsnContentException`: `UnexpectedMessage` for another application tag or a
  `msg-type` that disagrees with it, `UnsupportedVersion` for a `pvno`, `tkt-vno` or
  `authenticator-vno` other than 5, and `Malformed` for everything else (truncation,
  bytes left over, a missing field, an integer too large for its 32-bit field).
  `Microseconds` is read as any 32-bit integer, not checked against 0..999999.
- **`KerberosString` is UTF-8 in a primitive GeneralString.** It is written as an OCTET
  STRING under a one-byte stand-in tag whose byte is then replaced with GeneralString's
  `0x1B`; a constructed GeneralString is `Malformed`.
- **`KerberosTime` drops fractions of a second**; microseconds travel in their own fields.
- **`KerberosFlags` are 32 bits, bit 0 the most significant**, written as a five-byte BIT
  STRING as MIT writes them; a shorter one reads as zero-padded and bits past 32 are
  ignored, as MIT does.
- **A negative `UInt32` reads as its two's complement**, because some implementations
  write nonces and sequence numbers signed; it is written back unsigned.
- **An absent optional field is `null`; an absent optional list is empty**, and an empty
  list is written as absent, which is how MIT writes them.
- **`EncKDCRepPart` keeps the tag it was read with.** MIT's KDC puts `EncTGSRepPart`
  (`[APPLICATION 26]`) inside an AS-REP, so the decoder accepts 25 or 26 in either reply
  and `ReplyType` records which, for `Encode` to write it back.
- **Keys are `KerberosKey`, zeroed on `Dispose`.** The parts that carry one
  (`KerberosEncryptedKdcReplyPart`, `KerberosAuthenticator`, `KerberosEncryptedApReplyPart`)
  are `IDisposable`; a part that fails to decode after its key was read disposes the key
  before the exception leaves; writing a key does not copy its bytes into a second array,
  and the writer's buffer is cleared after `Encode`, whose result the caller zeroes.
- **`kvno` is a `UInt32`**, as RFC 4120 declares it: a Windows read-only DC puts its
  number in the upper 16 bits, which an `Int32` would refuse.
- **`Encode` refuses a message type the structure is not.** A `KerberosKdcRequest`,
  `KerberosKdcReply` or `KerberosEncryptedKdcReplyPart` whose type names another message
  throws `InvalidOperationException` rather than writing a mislabelled message.

## Consequences

The KDC exchange and the GSS-API mechanism build on these types without touching ASN.1.
`EncTicketPart`, FAST (`PA-FX-FAST`, RFC 6113) and the other pre-authentication types
travel as opaque `KerberosPreAuthenticationData` bytes until a task needs to read them.
The readers and writers the field helpers take are static fields made with an explicit
`new`: a method group or a lambda that captures nothing is cached by the compiler behind
a null check, a branch in every message reader that would break the complexity gate.

## Alternatives considered

- **Write every message in hand-rolled TLV code.** More code to get wrong for what
  `System.Formats.Asn1`, part of the BCL, already does; only GeneralString needed a
  workaround.
- **Read DER only.** Stricter than MIT, and a KDC that sends valid BER would be refused
  where `kinit` succeeds.
- **Keep optional lists nullable** to tell an absent list from an empty one. MIT never
  writes an empty optional list, and a nullable list forces a null check on every caller.
