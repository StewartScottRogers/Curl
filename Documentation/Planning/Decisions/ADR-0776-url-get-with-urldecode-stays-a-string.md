# ADR-0776 — The URL handle's decoded get stays a string

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3197
- Decided by Claude under Stewart's delegation.

## Context

Upstream's `curl_url_get` with `CURLU_URLDECODE` writes the decoded bytes of a part into a C
string, so `%FF` comes back as the byte 0xFF. `ICurlUrlHandle.Get` and Libcurl's
`CurlUrlHandle.Get` answer a .NET `string`, decoded as UTF-8, so a decoded byte that is not
UTF-8 becomes U+FFFD and the byte is lost (report 26, case 9).

## Decision

`Get` keeps its `string` form and gains no `byte[]` overload. A caller who needs the raw bytes
of a part gets it without `CURLU_URLDECODE`: the text then keeps its `%XX` escapes, every byte
is recoverable from it, and turning the escapes into bytes is one call in the BCL
(`Uri.UnescapeDataString` for text, or a loop over the escapes for bytes). A native C export
of `curl_url_get`, if one is built, decodes from that escaped text into the caller's buffer, so
nothing is lost at the engine boundary either.

## Consequences

- The managed API stays one `Get` per part, as every other managed Libcurl call answers text.
- A decoded get of a part holding bytes that are not UTF-8 still answers U+FFFD for them, as it
  does now; that is the documented cost of the string form.
