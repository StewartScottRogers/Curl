# Curl.Extension.ObliviousDoh.UnitLibrary

Oblivious DoH lookups (ADR-0999, ADR-1127): an `IDnsResolver` that encrypts each DNS query to a target's HPKE key
and POSTs it through a proxy, as RFC 9230 lays out. Tested against RFC 9230, not against curl.

- `ObliviousDohExtension`: the `oblivious-doh` description, the options `--odoh-proxy <URL>` and
  `--odoh-target <URL>` (given together), and `ActivateResolver`, registered in `Curl.Extension.Composition`'s
  `ExtensionResolverHookCatalogue`. `ObliviousDohResolverHook` builds the resolver over `Curl.Networking`'s
  `TcpConnector` and `SslStreamTlsProvider`, resolving the proxy's and target's own names with curl's resolver.
- `ObliviousDohEndpoints`: the proxy URL each query goes to (`targethost` and `targetpath` added, section 4.1)
  and the configs URL, `/.well-known/odohconfigs` on the target.
- `ObliviousDohResolver`: fetches and keeps the target's first usable config, then sends an AAAA and an A query
  per name (DNS codec from `Curl.Networking`), AAAA addresses first. Any failure gives no addresses.
- `Wire/`: `ObliviousDohConfig` (section 5 configs, key ID of section 6.1), `ObliviousDohMessage`,
  `ObliviousDohPlaintext` (zero padding to 128-octet blocks, RFC 8467), and the vector reader and writer.
- `Encryption/`: `ObliviousDohQuery` seals a query (section 6.2, HPKE from `Curl.Cryptography`) and opens its
  response (section 6.4) with `ResponseAead`.
- `Http/`: `ObliviousDohHttpClient` sends one HTTP/1.1 request per connection with `Connection: close`;
  `ObliviousDohHttpResponseParser` reads chunked, `Content-Length` or close-delimited bodies.
