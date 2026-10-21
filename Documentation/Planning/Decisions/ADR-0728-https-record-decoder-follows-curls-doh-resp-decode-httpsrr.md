# ADR-0728 — The HTTPS record decoder follows curl 8.22.0's `doh_resp_decode_httpsrr`

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2924
- Decided by Claude under Stewart's delegation.
- Amends: ADR-0312

## Context

Porting upstream unit program 1658 (`tests/unit/unit1658.c`) showed that
`ServiceBindingRecordDecoder` read an HTTPS record by RFC 9460's letter where curl 8.22.0's
`doh_resp_decode_httpsrr` (`lib/vdns/doh.c`) and `Curl_httpsrr_set` (`lib/vdns/httpsrr.c`) read
it otherwise. curl:

- refuses a target name whose text (up to a NUL) holds a space, a control character or DEL
  (`Curl_junkscan`), with `CURLE_WEIRD_SERVER_REPLY`;
- refuses SvcParam keys that do not rise strictly, with `CURLE_WEIRD_SERVER_REPLY`;
- reads SvcParams only while 4 or more bytes remain, and ignores 1 to 3 trailing bytes;
- accepts an empty `alpn` value and an empty alpn-id, and refuses only an alpn-id that runs past
  the value, with `CURLE_BAD_CONTENT_ENCODING`;
- refuses an empty `ech` with `CURLE_BAD_FUNCTION_ARGUMENT`;
- returns `CURLE_OUT_OF_MEMORY` for every target name it cannot decode, because the decode's own
  code is discarded.

## Decision

The decoder does what curl does in each of these cases. `ServiceBindingFailure` gains
`TargetNameHasControlCharacter`, `ParametersOutOfOrder` and `BadApplicationProtocolList`, so
every failure maps to exactly one of curl's codes (documented on each member), and `Truncated`
now means only data of 2 bytes or fewer. The port renders a decoding as unit1658's `rrresults`
does, mapping alpn identifiers to curl's ALPN ids there; the record keeps the identifiers as
text.

## Consequences

`DohDnsResolver.ResolveHttpsRecordAsync` accepts and refuses the same records curl's DoH
resolver does, so `--ech` finds a configuration in the same answers. All 30 rows of unit1658
match, and case 1658 is on the ratchet.
