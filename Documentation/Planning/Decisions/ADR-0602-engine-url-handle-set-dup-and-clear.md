# ADR-0602 — The managed engine's URL handle: `curl_url_set`, `curl_url_dup` and clearing a part

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2437.

## Context

ADR-0601 gave `ManagedCurlUrlHandle` every get but a `Set` that parsed a whole URL only; setting or
clearing any other part, relative URLs and `Duplicate` threw `NotSupportedException`. Upstream's
`curl_url_set` (curl 8.22.0, `lib/urlapi.c`) sets each part with its own checks and the `CURLU_*`
set flags, resolves a relative URL against the one held, and clears a part given a null value.
No upstream source or real curl was reachable from the lane, so the rules below follow upstream's
`curl_url_set`, `Curl_is_absolute_url` and `redirect_url` as written, not a measurement.

## Decision

1. **A null value clears the part** (`UrlPartSetter.Cleared`): the whole URL clears every part, the
   scheme also forgets it was guessed. A part upstream does not define is `CURLUE_UNKNOWN_PART`,
   with or without a value.
2. **Each part is set as upstream sets it** (`UrlPartSetter.Set`), the handle unchanged on failure:
   - the scheme: 1 to 40 characters (else `CURLUE_BAD_SCHEME`), one libcurl supports - `file` or one
     Curl's URL parser has a default port for - unless `CURLU_NON_SUPPORT_SCHEME` (else
     `CURLUE_UNSUPPORTED_SCHEME`, checked before the characters), then a letter followed by letters,
     digits, `+`, `-` or `.` (else `CURLUE_BAD_SCHEME`); stored in lower case and no longer guessed;
   - the port: decimal digits only, at most 65535 (else `CURLUE_BAD_PORT_NUMBER`), stored without
     leading zeros;
   - the host clears the zone id; empty is `CURLUE_BAD_HOSTNAME` unless `CURLU_NO_AUTHORITY`; under
     `CURLU_URLENCODE` it is stored escaped; otherwise a host holding `/`, `?`, `#`, `@` (or `:`
     outside brackets), or one `CurlUrl` does not accept as `http://host/`, is `CURLUE_BAD_HOSTNAME`,
     and a bracketed IPv6 address keeps its `%25` zone id apart;
   - the path gets a leading `/` when it has none;
   - the query under `CURLU_APPENDQUERY` is joined to a non-empty query with one `&` (none when it
     already ends in `&`); an empty query set is a bare `?`.
   Under `CURLU_URLENCODE` every UTF-8 byte but `A-Z a-z 0-9 - . _ ~` becomes an upper-case `%XX`,
   except a path's `/`, a query's space (`+`) and, under `CURLU_APPENDQUERY`, the query's first `=`.
   Without it the text is stored as given, its percent escapes' hex digits lower cased, as upstream's
   "make sure percent encoded are lower case" loop does.
3. **A whole URL**: empty or over 8,000,000 characters (`CURL_MAX_INPUT_LENGTH`) is
   `CURLUE_MALFORMED_INPUT`. It is absolute when it starts with a scheme and `:` (followed by `/`
   under `CURLU_GUESS_SCHEME` or `CURLU_DEFAULT_SCHEME`); otherwise, when the handle's URL can be got,
   the reference is resolved as `redirect_url` does (`UrlRelativeReference`: spaces and bytes outside
   ASCII encoded, then `//` keeps the scheme, `/` the authority, `#` all but the fragment, `?` the
   path, anything else the path to its last `/`) and the result parsed. A scheme libcurl does not
   support is `CURLUE_UNSUPPORTED_SCHEME` unless `CURLU_NON_SUPPORT_SCHEME`.
4. **`CURLU_ALLOW_SPACE`**: `CurlUrl` refuses spaces, so the text is parsed with each space as `%20`
   and, when the text held no `%20` of its own, the spaces are put back in every part but the host.
   A URL holding both a space and `%20` keeps both as `%20`; this is the one place the handle's get
   can differ from upstream's, which stores the space as is.
5. **`Duplicate` is a new handle over the same parts**: `UrlHandleParts` is an immutable record, so
   the two handles change apart.

## Consequences

Libcurl's `CurlUrlHandle` (BL-2438) can set, clear and duplicate through the real engine. Windows'
drive-letter rule in `Curl_is_absolute_url` (`c:` is not a scheme) is left out of
`UrlRelativeReference.IsAbsolute`; `CurlUrl` itself still applies it when the text is parsed.
Upstream's `test1560` was not run against the handle: its expectations are not in the conformance
ratchet, which drives the tool, not the library.
