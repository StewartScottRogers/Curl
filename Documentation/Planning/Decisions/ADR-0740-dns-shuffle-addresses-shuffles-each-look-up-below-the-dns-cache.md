# ADR-0740 — CURLOPT_DNS_SHUFFLE_ADDRESSES shuffles each look-up's answer below the DNS cache

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2922
- Decided by Claude under Stewart's delegation.

## Context

`CURLOPT_DNS_SHUFFLE_ADDRESSES` was stored in `EasyNetworkOptions.DnsShuffleAddresses` and read by
nothing. curl 8.22.0 shuffles in `dns_shuffle_addr` (`lib/vdns/dnscache.c`), a Fisher-Yates shuffle
from the last address down using `Curl_rand`, called when a DNS cache entry is made from a resolved
address list, so the shuffled order is the one cached. Upstream case 1608 (`tests/unit/unit1608.c`)
shuffles an eight-entry list up to ten times and fails when the head never changes; it needs the
`unittest` and `shuffle-dns` features.

## Decision

1. `Curl.Networking`'s `DnsAddressShuffle.Shuffle` is the Fisher-Yates shuffle, its random source
   injected; production draws from `RandomNumberGenerator.GetInt32`, as `Curl_rand` is a strong source.
2. `ShufflingDnsResolver` wraps the resolver the option group's connectors look names up through, and
   `EngineComposition.LookUpsOf` puts it there only when `DnsShuffleAddresses` is set. The connectors
   cache what the resolver answers, so the shuffled order is cached, as upstream's is. With the option
   off nothing is wrapped and the order is unchanged.
3. `--resolve` (`CURLOPT_RESOLVE`) entries are not shuffled yet: they enter `TcpConnector`'s cache
   without a resolver. Upstream builds those entries through the same entry constructor, so they are
   shuffled there; that is left as follow-up work.
4. `UpstreamCurlPlatform` keeps `shuffle-dns` off, as for every libcurl build feature; the harness
   reports it to case 1608 alone, beside `unittest`, through `UpstreamCaseScreening`'s per-program
   feature table, since the ported program runs the very shuffle the feature names.

## Consequences

A library handle with the option on tries a host's addresses in a random order, and case 1608 is on
the ratchet. The curl tool has no option for it, so its behaviour is unchanged.
