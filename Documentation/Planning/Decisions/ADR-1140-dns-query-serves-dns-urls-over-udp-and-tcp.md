# ADR-1140: The DNS query extension serves dns: URLs over UDP and TCP, chosen by --dns-transport and --dns-json

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4337
- Decided by Claude under Stewart's delegation

## Context

BL-4336 gave `Curl.Extension.DnsQuery` its RFC 1035 codec and its text and JSON writers (ADR-1124). BL-4337 makes it
a registered extension: a `dns:` URL sends a query and writes the answer. Upstream curl has no `dns:` scheme, so this
is tested against RFC 1035 (November 1987) and RFC 4501 (May 2006), not against curl, and never changes what curl does
with any other URL. Several choices had no specification to settle them: the option names, how a handler that the
engine builds once learns per-transfer options, what happens with no server in the URL, the retry policy over UDP, and
the exit codes.

## Decision

- **Scheme.** `dns`, read as RFC 4501 section 2 lays it out: `dns://[server[:port]]/name[?type=T;class=C]`. The port
  defaults to 53. The name is percent-decoded and then read in presentation format, so `%5C.` is a literal dot in a
  label. The query elements `TYPE=` and `CLASS=` are matched in either case and take a mnemonic or RFC 3597's `TYPEn`
  and `CLASSn`; they are separated by `;` as RFC 4501 writes them, and `&` is accepted too because `?type=A&class=IN`
  is how most people write a query. Type A and class IN are the defaults. A URL with no name, or with any other query
  element, fails with exit 3. Every query sets RD, as dig does.
- **No server in the URL.** RFC 4501 lets `dnsauthority` be left out, meaning "the client's default server". The
  handler then queries the first server `Curl.Networking`'s `SystemDnsServers.List` gives (the system configuration, as
  c-ares reads it), and fails with exit 6 when there is none. Curl's command line parses `dns:///name` as curl parses
  every non-file URL with three slashes, as host `name` and an empty path, so the handler answers it with exit 3 ("The
  dns: URL names no DNS name to query"). The server-less form is reachable only through hosts that parse URLs allowing
  an empty host; the handler supports it so the scheme is complete.
- **Options.** `--dns-transport <name>` names the transport: `udp` (the default) or `tcp`, in either case; any other
  name fails the transfer with exit 2, naming the transports. `--dns-json` writes the answer as one line of JSON
  (ADR-1124's writer) followed by a line feed, in place of dig-like text. The names are prefixed `dns-` so they cannot
  meet a curl option.
- **How the handler sees them.** Extension handlers are built once from `ProtocolHandlerServices` and see only the
  transfer's `ITransferContext`, so the options reach the handler as `--coap-observe` does (ADR-1064): a transfer hook,
  activated when either option is given, wraps a `dns://` transfer's context in a `DnsQueryContext` carrying the
  choice. The hook is registered in `ExtensionTransferHookCatalogue.cs`.
- **Transport selection.** `Transport/DnsQueryTransportSelection.cs` holds every transport's name and the transport it
  gives over curl's connection services, so DNS over TLS, QUIC and HTTPS (BL-4338, BL-4339) each add one name and one
  case there.
- **UDP** (RFC 1035 section 4.2.1). The query goes in one datagram through curl's datagram connector. It is sent up to
  3 times, waiting 5 seconds on the transfer's `TimeProvider` after each, as dig's defaults `+tries=3 +timeout=5`.
  A datagram that is not a response carrying the query's message ID is ignored. An answer with TC set is asked again
  over TCP, to the same server and port. No answer after the third attempt fails with exit 28.
- **TCP** (RFC 1035 section 4.2.2). The query and the answer are each prefixed by their length as two octets in
  network order. The connection closing before the whole answer fails with exit 56. `--max-time` bounds the wait
  through the transfer's cancellation.
- **Exit codes.** The connectors' own failures pass through (6 for a server name that does not resolve, 7 for no
  connection). An answer that does not decode, or whose message ID is not the query's, fails with exit 8. Any response
  code, NXDOMAIN and SERVFAIL included, is written and ends with exit 0, as dig does: the answer is the output asked for.

## Consequences

- `curl dns://9.9.9.9/example.com?type=AAAA` and `curl --dns-transport tcp --dns-json dns://9.9.9.9/example.com` work
  as a registered extension, listed by `curl --extensions` and `curl --ai-help extensions`; with `CURL_EXTENSIONS=0` a
  `dns://` URL fails as curl fails any unsupported scheme (exit 1, `Protocol "dns" not supported`).
- Left out, with why: EDNS(0) (RFC 6891) is not sent, so UDP answers stay within 512 octets and larger ones arrive
  over the TCP retry, which is what RFC 1035 alone specifies; DNSSEC validation, zone transfers (AXFR over TCP needs
  several messages) and the encrypted transports are separate work (BL-4338, BL-4339 for the transports).
