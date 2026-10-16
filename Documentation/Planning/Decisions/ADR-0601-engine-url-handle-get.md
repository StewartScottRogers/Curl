# ADR-0601 — The managed engine's URL handle: `curl_url_get` over `CurlUrl`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2436.

## Context

ADR-0506 puts libcurl's URL API (`curl_url`, `_get`, `_set`, `_dup`) behind the seam's
`ICurlUrlHandle`, built on Curl's own `CurlUrl` parser, and `ManagedCurlEngine.CreateUrl` threw
`NotSupportedException`. `CurlUrl` is a parsed transfer URL: it holds every part, but merges the
written port with the scheme's default and does not say whether the scheme was guessed. Upstream's
`curl_url_get` (curl 8.22.0, `lib/urlapi.c`) needs both.

## Decision

1. **`ManagedCurlUrlHandle` holds `UrlHandleParts`**, one nullable field per part as upstream's
   `struct Curl_URL` holds them, so BL-2437's setters can set and clear each one. `CreateUrl` gives
   an empty one: its URL is `CURLUE_NO_HOST`, its scheme `CURLUE_NO_SCHEME`, its path `/`.
2. **The written port is recovered from the URL's text** when `CurlUrl` reports the default port,
   and stored without leading zeros (`:080` reads `80`), as upstream regenerates its port string.
   `http://h:/` has none. A scheme's default port is the one `CurlUrl` gives `scheme://localhost/`,
   so the parser's table stays the one source.
3. **A scheme is guessed** when the text does not start with `scheme:/`. `CURLU_NO_GUESS_SCHEME`
   then makes the scheme `CURLUE_NO_SCHEME` and leaves `scheme://` out of the URL.
4. **Get follows upstream's `curl_url_get` part by part**: a missing part answers its `CURLUE_NO_*`
   code; a bare `?` or `#` gives the empty query or fragment only under `CURLU_GET_EMPTY`, in the
   part and in the URL; `CURLU_DEFAULT_PORT` gives the scheme's default when none is written and
   `CURLU_NO_DEFAULT_PORT` hides a written default (neither without a scheme); `CURLU_DEFAULT_SCHEME`
   builds the URL with `https` when no scheme is set. The transforms take upstream's precedence:
   `CURLU_URLDECODE` (a query's `+` first becoming a space, a byte below 0x20 failing with
   `CURLUE_URLDECODE`), else `CURLU_URLENCODE` (`urlencode_str`: control bytes, 0x7f and up as `%XX`,
   a space as `%20`, or `+` in a query and after a `?`), else, for the host, `CURLU_PUNYCODE` or
   `CURLU_PUNY2IDN` through the BCL's `IdnMapping` (the Schannel build has IDN), a name that does not
   convert failing with `CURLUE_BAD_HOSTNAME`. In the whole URL, `CURLU_URLENCODE` escapes a host
   name as `curl_easy_escape` does and an IPv6 host keeps its brackets and adds `%25` and its zone id.
5. **A `file` URL is rebuilt as `file://`, its path, query and fragment**, as curl 8.22.0's
   `curl_url_get` does; it has no host and no port.
6. **This task's `Set` parses a whole URL only**, which is what a get needs to be tested through the
   seam: `CURLU_DEFAULT_SCHEME` (`https`, winning over a guess), `CURLU_GUESS_SCHEME` (else
   `CURLUE_BAD_SCHEME` for a URL with no scheme), `CURLU_PATH_AS_IS` and `CURLU_DISALLOW_USER`, each
   `CurlUrlRejection` mapped to its `CURLUE_*` code and the handle unchanged on failure. Setting or
   clearing any other part, relative URLs, the other set flags and `curl_url_dup` are BL-2437's and
   throw `NotSupportedException` until then.

## Consequences

Libcurl's `CurlUrlHandle` (BL-2438) now works over the real engine for parsing and every get.
Upstream's `test1560` was not run against the handle here: its expectations are not in the
conformance ratchet, which drives the tool, not the library.
