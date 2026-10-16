# ADR-0508 — How `EasyHttpOptions` shapes libcurl's HTTP options

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2299.

## Context

ADR-0506's plan row `EasyHttpOptions` lists libcurl's HTTP `CURLOPT_*` options. A few of them do
not map one-to-one onto an immutable record: pairs that set the same state, options that append
or act rather than set, a `curl_mime` handle, and a pointer to another easy handle.

## Decision

- `CURLOPT_FOLLOWLOCATION` is the enum `FollowLocation` (0 and `CURLFOLLOW_ALL`, `_OBEYCODE`,
  `_FIRSTONLY`), not a `bool`: libcurl 8.13.0 made it a three-mode `long`.
- `CURLOPT_POSTFIELDS` and `CURLOPT_COPYPOSTFIELDS` share `PostFields`
  (`ReadOnlyMemory<byte>?`): the record holds the bytes, so "copy" and "point at" are the same
  state. `CURLOPT_POSTFIELDSIZE` and `_LARGE` share `PostFieldSize` (`long`).
- `CURLOPT_COOKIEFILE` is `CookieFiles` (a list): each setting adds a file. `CURLOPT_COOKIELIST`
  is `CookieList`, the lines and commands in the order given; the engine applies them.
- `CURLOPT_MIMEPOST` and `CURLOPT_HTTPPOST` each hold an `EasyMimeBody`, the data model of
  `curl_mime`: `EasyMimePart`s with name, file name, type, encoder, headers, sub-parts and one
  `EasyMimeData` source - `EasyMimeBytes`, `EasyMimeFile` or `EasyMimeCallback` (read and seek
  delegates from ADR-0507; `freefunc` has no counterpart, the closure's lifetime ends it).
- `CURLOPT_STREAM_DEPENDS` and `_E` share `StreamDependency`, an `EasyStreamDependency(object
  Parent, bool Exclusive)`. `Parent` is `object` because the easy handle type belongs to
  `Libcurl` and `Curl.Api`, which this seam must not reference.
- `CURLOPT_HTTP_VERSION` defaults to `CURL_HTTP_VERSION_NONE` and `CURLOPT_ALTSVC_CTRL` to 0, as
  curl 8.22.0's `docs/libcurl/opts` state.

## Consequences

The engine (BL-2307) reads one property per state; `Libcurl`'s `SetOption` maps both members of
each pair onto the same property and appends for `COOKIEFILE` and `COOKIELIST`. `Parent` needs a
type check where it is read.
