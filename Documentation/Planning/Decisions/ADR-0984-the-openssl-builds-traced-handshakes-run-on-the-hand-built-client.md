# ADR-0984: The OpenSSL build's traced handshakes run on the hand-built client

- Status: Accepted
- Date: 2026-10-28
- Decided by Claude under Stewart's delegation (BL-4010)

## Context

curl 8.18.0's OpenSSL build writes one `-v` line per handshake message (`TLSv1.3 (OUT), TLS
handshake, Client hello (1):` then `} [n bytes data]`, through `Finished (20):`; CGR-226).
ADR-0983 made the hand-built client report those messages as `TlsMessageEvent`s, but a
connection no row of ADR-0140's table routes runs on `SslStream`, which shows none of them, the
TLS 1.3 ones being encrypted. The trace must not be made up: no length is printed for a message
nobody saw.

Three ways were weighed:

1. Route every OpenSSL-build connection to the hand-built client. It matches curl's OpenSSL
   build most closely, but changes the TLS stack of every transfer off Windows at once, which
   the lanes, testing only on Windows, cannot check before CI does.
2. Print no trace for `SslStream` connections. Leaves CGR-226 open.
3. A routing row: in the OpenSSL build, a connection whose handshake messages are traced
   (`-v` or `--trace`) runs on the hand-built client.

What `SslStream` gives that the hand-built client lacks costs the OpenSSL build nothing here:
the system client-certificate store (`CurrentUser\MY\...`) is the Schannel build's alone, and
curl's OpenSSL build checks revocation only through `--crlfile` and `--cert-status`, both of
which the hand-built client already honours (ADR-0197, ADR-0191).

## Decision

- Option 3. `TlsClientOptions.TracesHandshakeMessages` says the trace is asked for, and
  `TlsClientRouting.ReasonInBuild` holds a row after the stapled-revocation one:
  `!matchesSchannelBuild && TracesHandshakeMessages`, whose reason is "the OpenSSL build traces
  the handshake messages, which SslStream does not show" (the diagnostic log's route reason).
- The Schannel build never takes the row: curl's Schannel build prints no message trace.
- Setting the option from `-v`, `--trace` and `--trace-ascii` in the engine's option mapping is
  BL-4047's (filed with this ADR), so this decision lands the routing alone.

## Consequences

- Off Windows, `-v` and `--trace` run the handshake on the hand-built client and print
  OpenSSL's message trace from what it saw; without them the transfer keeps `SslStream`.
- A server only one of the two clients can talk to answers differently with and without `-v`;
  the hand-built client is the one already carrying every OpenSSL-only option, so that is
  accepted until option 1 can be measured on Linux and macOS.
