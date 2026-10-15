# ADR-0494: CURL_HSTS_HTTP learns HSTS over plain http only behind the harness's seam

- Status: Accepted
- Date: 2026-10-14
- Task: BL-2250
- Decided by Claude under Stewart's delegation

## Context

Upstream case 446 sends two `Strict-Transport-Security` headers through the http-proxy as plain
`GET http://...` and verifies the `--hsts` file curl writes back. curl's release build learns HSTS
only over a TLS connection; its debug build also learns it over plain `http` when `CURL_HSTS_HTTP`
is set (`lib/http.c`, `#ifdef DEBUGBUILD`, tested with `getenv`, so any value, empty included). In
both builds the header goes through the same `Curl_hsts_parse` call, so an illegal one writes the same
`Illegal STS header skipped` line. ADR-0493 already stands in for the case's `CURL_TIME`.

Curl refused an `http` origin's header in two places: `HttpProtocolHandler.StoreHsts` and
`HstsTransferPolicy.StoreFromResponse`.

## Decision

- The HTTP handler hands every `Strict-Transport-Security` header to `HttpRequestOptions.HstsStore`,
  whatever the origin's scheme; the store decides whether an `http` origin's header is learned. This
  keeps the decision in one place, and the illegal-header line follows the store's answer as curl's
  does.
- `HstsTransferPolicy` takes `learnsOverHttp` (off by default). Off, an `http` origin's header
  returns `true` and stores nothing, as before; on, it is learned as an `https` one is.
- `CurlCommandRunner` takes `hstsLearnsOverHttp` and builds its policy with it. The dialing
  `CurlComposition.CreateRunner` takes `honoursHstsHttpVariable` (off by default) and sets it when
  `CURL_HSTS_HTTP` is set (`CurlComposition.LearnsHstsOverHttp`).
- The conformance tests turn the seam on. The shipped executable never does, so `curl` ignores
  `CURL_HSTS_HTTP` as curl's release build does.

## Consequences

- Case 446 passes and joins the ratchet; its `this.hsts.example.` loses its trailing dot in the file,
  as `HstsCache` already drops it.
- `IHstsStore` implementations now see `http` origins too and must ignore them unless they mean to
  learn them; `HstsTransferPolicy` and `ClockOverridingHstsStore` (which passes through) do.
