---
id: BL-687
title: Encode and decode Kerberos V5 messages with System.Formats.Asn1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-685]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-687 — Encode and decode Kerberos V5 messages with System.Formats.Asn1

## Goal

`Curl.Kerberos.UnitLibrary` encodes and decodes, in DER with the BCL's `System.Formats.Asn1`, the Kerberos V5 messages a client needs: AS-REQ, AS-REP, TGS-REQ, TGS-REP, AP-REQ, AP-REP, KRB-ERROR, and the structures inside them (Ticket, EncKDCRepPart, Authenticator, EncAPRepPart, PA-DATA including PA-ENC-TIMESTAMP and PA-ETYPE-INFO2, PrincipalName, KerberosTime, EncryptedData, Checksum).

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). RFC 4120 section 5 and Appendix A (the ASN.1 module, application tags, explicit context tags, `KerberosString` as GeneralString, `KerberosTime` as GeneralizedTime without fractions).
- Pure code: bytes in, typed records out and back. A malformed or unexpected message is a typed failure, never an unhandled `AsnContentException`.
- Test data: messages captured once from an MIT KDC (`kinit` against a local `krb5kdc`, captured with `Record-CurlExchange.ps1 -NoServer` or a packet capture) committed as test bytes with their source in a comment, or messages built from RFC 4120's definitions in the test.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` round-trip every listed message type byte for byte, decode at least one captured AS-REP and one KRB-ERROR (`KDC_ERR_PREAUTH_REQUIRED` with PA-ETYPE-INFO2), and reject a wrong application tag and a truncated message with the typed failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: one library and
  its tests, the plan being RFC 4120's ASN.1 module itself. A `code-reviewer` pass ran
  before commit.
- Test bytes: a real exchange with MIT Kerberos 1.22.1, the KDC BL-688 set up under WSL
  (`~/krbtools`), with alice made `+requires_preauth`. WSL runs NAT networking and the
  Windows firewall blocks WSL-to-host traffic, so a Windows-side relay could not work;
  `strace -xx -e trace=sendto,recvfrom` (unpacked with `apt-get download strace
  libunwind8` + `dpkg -x`, no sudo) recorded each UDP datagram of `kinit -f alice` and
  `kvno HTTP/server.example.test`: AS-REQ, KRB-ERROR (`KDC_ERR_PREAUTH_REQUIRED` with
  `PA-ETYPE-INFO2`), AS-REQ with `PA-ENC-TIMESTAMP`, AS-REP, TGS-REQ, TGS-REP
  (`RecordedKerberosMessages.cs`). The tests decrypt the chain with BL-686's crypto:
  alice's password decrypts the timestamp and the AS-REP part, its session key decrypts
  the TGS-REQ authenticator, and the authenticator's checksum verifies over the
  re-encoded `KDC-REQ-BODY`. The TGS-REP's part is FAST-armoured (`PA-FX-FAST`), so it
  is round-tripped, not decrypted. AP-REP and `EncAPRepPart` (none in the exchange) are
  pinned to DER written by hand from RFC 4120.
- Found: MIT's KDC tags an AS-REP's encrypted part `EncTGSRepPart` (26); the decoder
  keeps the tag it read (`ReplyType`).
- Decisions (ADR-0163, decided under Stewart's delegation): write DER, read BER; typed
  `KerberosMessageError` (`Malformed`, `UnexpectedMessage`, `UnsupportedVersion`);
  UTF-8 GeneralStrings (written through a patched stand-in tag, since `AsnWriter` has no
  GeneralString writer); 32-bit flags; signed `UInt32` accepted; absent optional lists
  are empty. ADR-0162 is taken on `work/dark-factory` by lane 3, hence 0163.
- Quality gate: the compiler caches static method-group and non-capturing-lambda
  delegates behind a null check, which counted as branches and put the big readers at
  complexity 26. Readers and writers are therefore static fields made with explicit
  `new Func<...>(...)` (no cache), noted in `KerberosAsn1`'s remarks.
- `code-reviewer` findings fixed: a part that fails to decode after its key was read now
  disposes the key (and so does trailing data after any disposable value); `kvno` is
  `UInt32` (Windows RODC kvnos set the high bit); `AsnWriter` buffers are cleared after
  `Encode`; `Encode` refuses a mislabelled message type; `PeekType` requires a
  constructed tag; `Malformed`'s doc no longer claims `Microseconds` is range-checked.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0163 and its index row; no
  task in Doing names it.
- Verified: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Kerberos 355);
  `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary`: 100% line, 100% branch,
  353 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Kerberos encodes and decodes AS-REQ/REP, TGS-REQ/REP, AP-REQ/REP, KRB-ERROR and their inner structures in DER with System.Formats.Asn1, pinned to a recorded MIT exchange
