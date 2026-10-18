# ADR-0675 — A `CURLOPT_MIMEPOST` structure is read at each perform, and `curl_mime_free` unbinds it

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2626.

## Context

`Libcurl`'s `CURLOPT_MIMEPOST` setter stores the `CurlMime`'s `EasyMimeBody` as it is when set (ADR-0583).
libcurl instead binds the structure to the handle (`Curl_mime_set_subparts`) and reads it when the
request is sent, so upstream case 653 (a part's data changed between two performs) sent the first
data again (`Content-Length: 162`, not 179). `curl_mime_free` on a bound structure unbinds it, leaving the
handle's part `MIMEKIND_NONE`, which posts a zero-length body with no `Content-Type`; case 654 (the
structure freed after `curl_easy_duphandle`) still posted the parts from the original handle.

## Decision

1. `CurlEasy` remembers the `CurlMime` a successful `CURLOPT_MIMEPOST` set; a `NULL` MIME post or a
   `CURLOPT_HTTPPOST` (which libcurl then sends instead) forgets it, as does `curl_easy_reset`.
2. Each perform, and each `curl_easy_duphandle`, first writes the remembered structure's body as its parts
   are now into the options. A duplicate therefore holds a copy, as `Curl_mime_duppart` makes, and posts
   it whatever later happens to the original structure.
3. Once the structure is freed, the options carry no `MimePost` and an empty `HttpPost` form instead,
   which the engine already posts as a zero-length body with no `Content-Type` (BL-2599), the bytes of
   libcurl's unbound part. No `Curl.Api` or engine contract changes.
4. The setter itself still stores the body as it is when set, so `CurlEasy.Options` read before a
   perform keeps today's answer.

A handle performed through `CurlMulti` is not refreshed by this decision; the multi path reads the
options as the setter left them.

## Consequences

Upstream cases 653 and 654 pass. A `CURLOPT_MIMEPOST` of `NULL` is BL-2620's.
