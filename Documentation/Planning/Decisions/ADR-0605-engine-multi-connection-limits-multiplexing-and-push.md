# ADR-0605 — The engine multi's connection limits, multiplexing and server push

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2428.

## Context

ADR-0506 put `CURLMOPT_PIPELINING`, `MAXCONNECTS`, `MAX_HOST_CONNECTIONS`, `MAX_TOTAL_CONNECTIONS`,
`MAX_CONCURRENT_STREAMS` and `PUSHFUNCTION` on the seam as `MultiOptions`, and `ManagedMultiTransfer`
kept them without reading them: every added transfer started on the next perform (ADR-0594, ADR-0604,
pending always 0). The managed engine's transfers each run their own `IEasyTransfer.PerformAsync` over
the shared `ConnectionCache`, so the multi cannot see which connection a transfer will get before it
starts. No upstream source or real curl was reachable from the lane, so the rules below follow curl
8.22.0's `lib/multi.c`, `lib/url.c` and `lib/http2.c` as recalled, not a measurement.

## Decision

1. **A transfer that would pass a connection limit waits, pending.** Before a perform starts a transfer
   it counts the connections the running transfers hold, as one per transfer, except that under rule 2
   transfers to one origin that multiplex share connections, `MAX_CONCURRENT_STREAMS` to a connection.
   A transfer that cannot join a multiplexed connection with a stream free starts only when its origin
   (scheme, host and port of its URL, from `System.Uri`; the URL text when it does not parse) holds fewer
   than `MAX_HOST_CONNECTIONS` connections and the handle fewer than `MAX_TOTAL_CONNECTIONS`, each 0
   meaning no limit. Otherwise it stays pending: no perform, no socket for the socket callback or
   waitfds, no timeout, counted by `CURLMINFO_XFERS_PENDING` and not by `XFERS_RUNNING`. Each perform
   first moves the started transfers on, so a slot one frees goes to a pending one, in add order, in the
   same call.
2. **Who multiplexes.** Under `CURLPIPE_MULTIPLEX` a transfer multiplexes when its URL is `https` and its
   `CURLOPT_HTTP_VERSION` is not 1.0 or 1.1 (libcurl's default `CURL_HTTP_VERSION_2TLS` offers h2 by
   ALPN), or when it asks for HTTP/2 with prior knowledge. Plain-`http` HTTP/2 by upgrade does not: the
   connection is not known to multiplex before the first response. `CURLPIPE_HTTP1` alone is ignored,
   as libcurl has since 7.62.0.
3. **`MAX_CONCURRENT_STREAMS`** below 1 or past `int.MaxValue` is 100, as libcurl's setopt falls back.
4. **Server push.** `ManagedMultiTransfer.OfferPush(parent, pushed, headers)` is the engine's
   `push_promise`: it refuses (`CURL_PUSH_DENY`) when the handle is disposed, the parent is not in it,
   no push callback is set or the pushed transfer is null or in a multi handle already; otherwise the
   callback decides. `CURL_PUSH_OK` adds the pushed transfer, which starts on the next perform;
   `CURL_PUSH_ERROROUT` refuses it and ends the parent on the next perform with `CURLE_HTTP2`, as the
   failed nghttp2 callback fails the parent stream; every other answer refuses it.
5. **Left to follow-up tasks.** `MAXCONNECTS` needs the shared cache to close idle connections past a
   count, which `ConnectionCache` cannot do yet, so it is kept and not applied until BL-2468. The HTTP/2
   client in `Curl.Protocol.Http.UnitLibrary` still refuses push in its `SETTINGS`, so nothing calls
   `OfferPush` until BL-2469 wires `PUSH_PROMISE` to it.

## Consequences

- Applications that cap connections see transfers queue and `XFERS_PENDING` rise, as with libcurl.
- The connection count is the multi's own estimate; the shared cache may still reuse an idle
  connection for a transfer the estimate counts as new, which only makes the limit stricter.
- `Curl.Engine.UnitTests`' `ManagedMultiTransferTests.Limits.cs` pins the queueing and every push answer.
