# ADR-0445 — `-b name=value` strings stay with the first origin on a redirect

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1846, GF-0010).

## Context

Upstream test2015 runs `-b test=yes -L` and follows a redirect to another host; curl 8.21.0 sends
no `Cookie:` on the second request (GF-0010), while Curl sent `Cookie: test=yes`. libcurl adds
`CURLOPT_COOKIE` only when `Curl_auth_allowed_to_host` allows it: not a follow, or
`--location-trusted`, or the same host name, port and scheme as the first URL. That is the rule
`RedirectFollower` already uses to drop `-u` credentials and `-H Authorization:`/`Cookie:` on a
hop. A recording of the host-change case with `Record-CurlExchange.ps1` hung on this machine
(the redirect to `localhost`), so the rule is taken from libcurl's source and the GF-0010
measurement.

## Decision

- `HttpRequestOptions.SendsCookieStrings` (on by default) says whether a request sends the
  `-b name=value` strings. `RedirectFollower` turns it off on a hop whose scheme, host or port
  differs from the first URL's, unless `--location-trusted`, the same test that drops credentials.
- `ICookieStore.GetStoredCookieHeader` gives the stored cookies alone; its default is
  `GetCookieHeader`, for a store with no strings of its own. `HttpProtocolHandler` asks it when
  the flag is off. `Curl.Console`'s `CookieEngine` stores leave the strings out there.
- Stored cookies keep their own domain rules; two command-line URLs on different hosts each still
  get the strings, since neither is a follow.

## Consequences

Upstream test2015 matches. Any later `ICookieStore` that adds strings of its own must implement
`GetStoredCookieHeader`.
