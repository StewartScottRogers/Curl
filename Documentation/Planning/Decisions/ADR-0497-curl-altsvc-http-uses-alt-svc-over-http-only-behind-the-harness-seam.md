# ADR-0497: CURL_ALTSVC_HTTP uses Alt-Svc over plain http only behind the harness's seam

- Status: Accepted
- Date: 2026-10-14
- Task: BL-2263
- Decided by Claude under Stewart's delegation

## Context

Upstream cases 358 and 359 preload an `--alt-svc` file with an `h2` entry for a plain `http` origin,
fetch that origin twice with `--http2`, and expect both requests to go to the h2c server on
`%HTTP2PORT`. curl's release build looks up and learns Alt-Svc only for `https`; its debug build
does both for plain `http` too when `CURL_ALTSVC_HTTP` is set (`lib/url.c` for the lookup,
`lib/http.c` for the `Alt-Svc` header, both `#ifdef DEBUGBUILD` and tested with `getenv`, so any
value, empty included). The cases name the `Debug` feature for that alone.

Curl refused an `http` origin in two places: `AltSvcTransferCache.MatchFor` (the lookup) and
`HttpProtocolHandler.StoreAltSvc` (the header).

Cases 2400, 2401 and 2406 run against nghttpx's TLS front on `%HTTP2TLSPORT` and also name `Debug`,
but use nothing a debug build does differently.

## Decision

- As for HSTS (ADR-0494), the HTTP handler hands every `Alt-Svc` header to
  `HttpRequestOptions.AltSvcStore`, whatever the origin's scheme, and the store decides.
- `AltSvcTransferCache.OpenAsync` takes `usesOverHttp` (off by default). Off, an `http` origin's
  header is ignored with no `-v` line and no alternative is looked up for it, as before; on, an
  `http` origin is served as an `https` one is.
- `CurlCommandRunner` takes `altSvcUsesOverHttp` and opens each cache with it. The dialing
  `CurlComposition.CreateRunner` takes `honoursAltSvcHttpVariable` (off by default) and sets it when
  `CURL_ALTSVC_HTTP` is set (`CurlComposition.UsesAltSvcOverHttp`).
- The conformance tests turn the seam on, and report `Debug` to a case naming the `alt-svc` feature
  and setting `CURL_ALTSVC_HTTP`, and to a case whose command names `%HTTP2TLSPORT`. The shipped
  executable never turns the seam on, so `curl` ignores `CURL_ALTSVC_HTTP` as curl's release build
  does.
- `UpstreamCaseScreening.FindFeatureOfAnotherPlatform` (ADR-0488) counts a feature this run reports
  that no platform in `UpstreamCurlPlatform.All` reports - a harness stand-in such as `Debug` - as
  met on every platform, since the harness stands in for it there too. Without that, a listed case
  needing `Debug` and `http/2` would fail on Windows, where no platform's base features meet both.

## Consequences

- 359, 2400, 2401 and 2406 pass when run as the Linux build and join the ratchet; on Windows they
  stay `Inconclusive` for `http/2` (ADR-0495, ADR-0488). 358 passes the same way and is left to
  BL-2266 to list.
- `IAltSvcStore` implementations now see `http` origins too and must ignore them unless they mean to
  use them; `AltSvcTransferCache`, the only production one, does.
