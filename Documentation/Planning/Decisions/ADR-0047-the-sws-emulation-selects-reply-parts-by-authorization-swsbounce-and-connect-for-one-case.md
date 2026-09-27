# ADR-0047 — The sws emulation selects reply parts by authorization, `swsbounce` and `CONNECT`, taking every request to name its one case

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (task BL-265, 2026-09-26).

## Context

`SwsHttpServerConnector` in `Curl.Conformance.UnitLibrary` emulates upstream's `sws` in
memory. BL-146 chose the reply part from the request path alone. In the vendored test data 43
cases use `swsbounce` and 71 have a `<connect>` part, and the HTTP authentication cases rely on
sws moving the part number when it sees an `Authorization:` header.

Read in `tests/server/sws.c` at `curl-8_21_0` (fetched from GitHub for this decision):

- `sws_ProcessRequest` takes the test number from the number that starts the path's last
  segment (read with a limit of `INT_MAX`); over 10000, the part is its last four digits.
  Only when the path gives no number does it test for `CONNECT %s HTTP/%d.%d`; that marks the
  request as a `CONNECT` and leaves the part at 0 (for an IPv6 literal the test number becomes
  the last hex group). The test number then comes from the runner's command file. There is no
  "number after the last dot" rule at this tag.
- On the complete request, the first of these that `strstr` finds anywhere in it changes the
  part: `Authorization: Negotiate` sets it to a static counter plus one (the counter restarts
  from the request's part when the test number changes); `Authorization: Digest` adds 1000;
  `Authorization: NTLM TlRMTVNTUAAD` adds 1002; `Authorization: NTLM TlRMTVNTUAAB` adds 1001;
  `Authorization: Basic` adds 1 when the part is already 1000 or more. `Proxy-Authorization:`
  contains the same text, so it counts too. sws never reaches these rules for a request with a
  `Transfer-Encoding: chunked` header, or with a `Content-Length` it cannot read while it has
  no length yet: its header loop returns first.
- `service_connection`: when the previous reply contained `swsbounce` and the test number is the
  previous one, the part becomes the previous part plus one, whatever was asked for.
  `sws_send_doc` sets the bounce flag from each reply it reads (clearing it for the 404
  document) and records the part it served. Under `idle` and `stream` it returns before
  either, but `<servercmd>` applies to every request of a case, so such a case never sends a
  reply the bounce could change.
- `sws_send_doc` answers a `CONNECT` from `<connect>` / `<connectN>`. Its closing
  `req->open = persistent` overrides the `req->open = FALSE` that `sws_ProcessRequest` sets for
  an HTTP/1.0 `CONNECT` and for `Connection: close`, so neither closes the connection. Without
  `--connect` (plain `http` server) sws does not tunnel: it reads the next request on the same
  connection.

## Decision

1. The part number is chosen as read above: path, then the authorization rules, then the
   bounce; the section is `connect` for a `CONNECT` request with no test number in its path,
   `data` otherwise.
2. The emulation serves one case, so every request is taken to name it: the test number never
   changes, the Negotiate counter is never restarted after its first request, and a bounce
   always applies to the next request. Both states live in the selector the connector shares
   between its connections, as sws keeps them in statics.
3. A `CONNECT` reply keeps the connection open for the tunnelled request unless the usual
   close rules (`swsclose`, an empty part) apply. HTTP/1.0 and `Connection: close` do not close
   it, because sws's final assignment overrides both.

## Consequences

- Authentication and proxy cases get the reply upstream serves them from the plain `http`
  server. The `http-proxy` server (`sws --connect`, a real tunnel to a second server) is not
  emulated.
- The rules search the whole request, where sws's `strstr` on its buffer stops at a NUL in the
  body and can see bytes of a pipelined request after it; no vendored case depends on either.
- An IPv6 `CONNECT` target, whose last hex group sws would take as the test number, is served
  from this case like any other.

## Alternatives considered

- **Keep a test number per request and compare it**, as sws does. It would only differ for an
  IPv6 `CONNECT` target, where sws opens another case's file; the emulation has no other case
  to open, so the comparison would add state with nothing to decide.
