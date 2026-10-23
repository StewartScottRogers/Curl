# ADR-0796: CurlUrlHandle.GetBytes gives curl_url_get's part as the C ABI's raw bytes

- Status: Accepted
- Date: 2026-10-23
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0's `curl_url_get` answers a `char *`: with `CURLU_URLDECODE`, `http://a/%FF` gives the
path bytes `2F FF`, and `%C0%AF` and `%ED%A0%80` give their raw bytes too (BL-3236's differential
replay against libcurl 8.22.0, scenarios 1080 to 1100). The engine already carries such bytes as the
lone surrogates U+DC80 to U+DCFF (ADR-0697), but Libcurl's `CurlUrlHandle.Get` answers only a .NET
string, and a caller writing it as UTF-8 (the differential runner's managed target did) turns each
into U+FFFD (`EF BF BD`).

## Decision

`CurlUrlHandle.GetBytes(part, flags, out byte[]? value)` is `curl_url_get` as the C ABI returns it:
the engine's text mapped back through `UrlByteText.ToBytes`, so every byte libcurl would give is
given. `Get` keeps answering the string, so callers that want text are unchanged. It is a separate
name rather than a `Get` overload because `out var` callers would make an overload ambiguous. The
differential runner's managed target reads parts through `GetBytes`.

## Consequences

- Scenarios 1080 to 1100 compare equal; text that is valid UTF-8 gives the same bytes as before.
- No engine contract changes: `ICurlUrlHandle` stays string-based, with ADR-0697's mapping.
