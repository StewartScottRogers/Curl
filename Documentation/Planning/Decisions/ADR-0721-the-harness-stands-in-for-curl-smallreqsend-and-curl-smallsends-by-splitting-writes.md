# ADR-0721: The harness stands in for `CURL_SMALLREQSEND` and `CURL_SMALLSENDS` by splitting each connection's writes

- Status: Accepted
- Date: 2026-10-21
- Decided by Claude under Stewart's delegation (BL-2832)

## Context

Upstream cases 363, 1294, 1295 and 5028 name the `Debug` feature because they set variables only
upstream's debug build reads. `CURL_SMALLREQSEND=N` cuts the first send of a request off after N
bytes, so the rest of the request follows in later sends; `CURL_SMALLSENDS=M` caps every send at M
bytes. Both change how curl's bytes are split into sends, never the bytes: the cases check that the
servers reassemble a request that arrives in pieces (363: a `CONNECT` and a `POST` in 8-byte pieces;
1294 and 1295: a long GET and POST cut after 128 and 100 bytes; 5028: a Gopher selector whose CRLF
arrives one byte at a time). Neither the curl tool nor libcurl reads these variables in a release
build, so Curl must not read them either.

## Decision

- The harness reports `Debug` to a case whose `<setenv>` sets either variable
  (`UpstreamConformanceTests.SetsSmallSendsVariable`, one line in the Debug stand-in table).
- `UpstreamSmallSendsFront`, the last and outermost stand-in front, wraps the runner's connector in a
  `SmallSendsConnector` when the case sets either variable to a positive number. Every TCP connection
  it makes is a `SmallSendsConnection`, whose first write goes to the server in pieces of at most
  `CURL_SMALLREQSEND` bytes and every write in pieces of at most `CURL_SMALLSENDS` bytes (the smaller
  when both apply). Every other member, and QUIC, passes through to the inner connection.
- The stand-in splits per connection's first write, not per request: for these four cases each
  request's first send is a connection's first write, or is covered by `CURL_SMALLSENDS` (363), so
  the servers see the request in the same pieces as from the debug build.
- Curl and Libcurl are unchanged.

## Consequences

- 363, 1294, 1295 and 5028 pass on Windows and are on `PassingUpstreamCases.txt`. They are HTTP and
  Gopher cases with no paths or OS text in their verified output, so they hold on Linux and macOS too.
- A future case that sets a small-send variable and makes several requests on one connection gets the
  first-write limit only on the first of them; such a case would extend `SmallSendsConnection`.
