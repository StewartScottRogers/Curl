# ADR-0695: URL bytes that are not UTF-8 are carried as the lone surrogates U+DC80 to U+DCFF

- Status: Accepted
- Date: 2026-10-18
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0's URL API works on bytes: `https://_%c0_` keeps the byte `0xc0` in its host, gives it
back raw from `curl_url_get` and as `%C0` under `CURLU_URLENCODE`, and `https://\xff.127.0.0.1`
under `CURLU_URLENCODE` gives `%FF` (lib1560's `get_url_list`). Curl's `ICurlUrlHandle` takes and
answers .NET strings, which cannot hold a lone byte, and the engine decoded every percent-escape as
UTF-8, so `%c0` became U+FFFD (encoded again as `%EF%BF%BD`). BL-2652's port wrote such C bytes
as Latin-1 characters, but Latin-1 is ambiguous: `ÿ` is also the character U+00FF, whose bytes libcurl
gives as `%C3%BF` (lib1560's `räksmörgås` rows depend on that).

## Decision

`Curl.Protocol.Abstractions`' `UrlByteText` is the one mapping between a URL's bytes and text:
valid UTF-8 is its characters, and each byte that is not part of valid UTF-8 is the lone surrogate
U+DC00 plus the byte (U+DC80 to U+DCFF, which no valid UTF-8 produces), as Python's
`surrogateescape` does. Back to bytes, a lone U+DC80 to U+DCFF is its byte and every other
character its UTF-8. `CurlUrlHost`'s percent-decoding and the engine's `UrlPartText` (decode,
encode and escape) go through it, so every byte survives a decode and encode. lib1560's port
writes such bytes the same way (`\uDCC0`, `\uDCFF`).

## Consequences

- lib1560's rows 123, 124 and 136 answer as libcurl does; text that is valid UTF-8 changes nothing.
- The curl tool's host for `http://a%80b/` is now `a\uDC80b` rather than `a�b`; a UTF-8 writer
  still prints the lone surrogate as U+FFFD, so its output bytes do not change.
- MSTest `DataRow` strings are UTF-8 in metadata and cannot hold a lone surrogate, so tests of these
  bytes are plain test methods.
