# ADR-0854: The release build uses only an https DoH URL

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3692
- Decided by Claude under Stewart's delegation

## Context

Third-party gap report issue CGR-163: `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x`
exits 6 with `curl: (6) Could not resolve host: foo.test` on curl's release builds and never contacts the DoH
server, as `lib/doh.c` limits the DoH sub-transfer to `https` (a debug build also allows `http`, which is why the
report's Windows reference, a debug build, used the server). curl's documentation says "The URL must be HTTPS."
Curl used an `http` DoH URL and sent the queries over plain HTTP.

Measured on 2026-10-26 with curl 8.21.0's Windows release build (`C:\Windows\System32\curl.exe`, Schannel):
`--doh-url http://127.0.0.1:21800/dns-query` and the scheme-less `--doh-url 127.0.0.1:21800/dns-query` both print
`* Could not resolve host: foo.test` under `-v`, open no connection, and end with exit 6 and
`curl: (6) Could not resolve host: foo.test`. The mingw curl 8.22.0 on the lane machines is a debug build and
reaches the server.

## Decision

`EngineComposition.CreateDnsResolver`, the resolver of the shipped `curl` and of every `Curl.Api` and `Libcurl`
handle, takes `allowsPlainHttpDohUrl` (off by default). Off, a DoH URL that `DohUrlOf` does not make an `https`
URL of (`IsHttpsDohUrl`: `http`, scheme-less, any other scheme or one that does not parse) gives the
`UnusableDohUrlResolver`, so every name but an IP literal or `localhost` fails with exit 6, as `--doh-url bogus`
already did (BL-642). `CreateDohResolver` itself still accepts `http`, and the conformance harness's dialing paths
(`DialingResolverOf`, `CurlComposition.DialingDohResolverOf`) keep using it, as they stand in for the debug build
upstream's DoH cases run on (BL-2928).

## Consequences

- The shipped `curl` matches the release builds: no DNS query goes out over plain HTTP.
- Upstream cases 2100, 2101 and 2117 still pass through the harness's debug stand-in.
- Tests that need a plain-HTTP DoH server through `CreateDnsResolver` pass `allowsPlainHttpDohUrl: true`.
