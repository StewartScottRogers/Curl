# DnsQuery requirement matrix: encrypted transports

Curl's `dns://` client (`Curl.Extended.Protocol.DnsQuery.UnitLibrary`) measured under `--dns-transport tls` against
RFC 7858 (DNS over TLS) and RFC 8310 where its profiles bind a client, and under `--dns-transport https` against
RFC 8484 (DNS over HTTPS), through the reference servers in `Curl.Conformance.Extended.UnitLibrary/DnsQuery/`
(BL-4681): `DnsReferenceServer` behind a TLS route for DoT, `Encrypted/DohReferenceServer` (HTTP/1.1) behind a TLS
route for DoH, and `Encrypted/DnsTlsRefusalServer` for a certificate curl must refuse. The cleartext transports have
their own matrix, [DnsQuery.md](DnsQuery.md). A case listed in `KnownFailures.txt` names the task that fixes it.

## RFC 7858 and RFC 8310 (DNS over TLS)

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| DnsQuery:RFC7858-3.1-1 | MUST | A client wanting privacy from a server establishes its TCP connection to port 853 unless agreed otherwise. | DnsOverTlsCases.Tls_DefaultPort853_CarriesTheLengthPrefixedQueryAfterTheHandshake |
| DnsQuery:RFC7858-3.2-1 | MUST | Once connected, the client completes the TLS handshake before it sends any DNS message. | DnsOverTlsCases.Tls_DefaultPort853_CarriesTheLengthPrefixedQueryAfterTheHandshake |
| DnsQuery:RFC7858-3.3-1 | MUST | Every DNS message over the TLS session carries the two-octet length field of RFC 1035 section 4.2.2. | DnsOverTlsCases.Tls_DefaultPort853_CarriesTheLengthPrefixedQueryAfterTheHandshake |
| DnsQuery:RFC7858-3.2-2 | SHOULD | A client remembers servers that do not support DNS over TLS and does not retry them for a while. | n/a: curl makes one connection per run and keeps no state between runs |
| DnsQuery:RFC7858-3.3-2 | SHOULD | A client pipelines its queries over one TLS session and matches answers by ID. | n/a: curl sends one query per transfer |
| DnsQuery:RFC7858-3.4-1 | SHOULD | A client reuses an established TLS session for later queries and uses TLS session resumption. | n/a: curl sends one query per transfer and closes the connection after its answer |
| DnsQuery:RFC7858-3.4-2 | SHOULD | A client follows RFC 7766's guidance on idle connections and closes them gracefully. | n/a: curl closes the connection as soon as its one answer arrives |
| DnsQuery:RFC8310-5-1 | MUST | Under the Strict profile a client that cannot authenticate the server does not send it queries (hard fail); `-k` chooses the Opportunistic profile. | DnsOverTlsCases.Tls_UntrustedCertificate_IsRefusedWithExit60AndNoCleartextFallback |
| DnsQuery:RFC8310-5-2 | MUST NOT | Under the Strict profile a client never falls back to cleartext DNS. | DnsOverTlsCases.Tls_UntrustedCertificate_IsRefusedWithExit60AndNoCleartextFallback |
| DnsQuery:RFC8310-9-1 | SHOULD | A client pads its queries with the EDNS(0) Padding option (RFC 7830, RFC 8467). | DnsOverTlsCases.Tls_Query_IsPaddedWithTheEdnsPaddingOption |

## RFC 8484 (DNS over HTTPS)

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| DnsQuery:RFC8484-4.1-1 | MUST | A POST carries the DNS query as its body and its `Content-Type` names the body's media type. | DnsOverHttpsCases.Https_DefaultPort443_PostsTheQueryAsAnApplicationDnsMessageBody |
| DnsQuery:RFC8484-4.1-2 | SHOULD | A client using `application/dns-message` uses DNS ID 0 in every request, for cache friendliness. | DnsOverHttpsCases.Https_Query_CarriesDnsId0 |
| DnsQuery:RFC8484-4.1-3 | SHOULD | A client sends an `Accept` header naming the response media types it understands. | DnsOverHttpsCases.Https_DefaultPort443_PostsTheQueryAsAnApplicationDnsMessageBody |
| DnsQuery:RFC8484-4.1-4 | MUST | A GET carries the query in the `dns` parameter as base64url with no padding. | n/a: curl's DoH client always uses POST, which section 4.1 leaves to the client's choice |
| DnsQuery:RFC8484-6-1 | MUST | A client implements the `application/dns-message` media type for requests and responses. | DnsOverHttpsCases.Https_DefaultPort443_PostsTheQueryAsAnApplicationDnsMessageBody |
| DnsQuery:RFC8484-4.2.1-1 | MUST | A 2xx response carries a DNS answer whatever its RCODE; the client reads it as one. | DnsOverHttpsCases.Https_2xxWithServfail_IsWrittenAsTheDnsAnswer |
| DnsQuery:RFC8484-4.2.1-2 | MUST | A non-2xx response holds no reply to the question; the client treats it as any HTTP client would. | DnsOverHttpsCases.Https_Non2xxStatus_IsNoAnswerAndExit22 |
| DnsQuery:RFC8484-5-1 | MUST | DoH is HTTPS: the client authenticates the server as RFC 2818 says before it sends a request. | DnsOverHttpsCases.Https_UntrustedCertificate_IsRefusedWithExit60BeforeAnyRequest |
| DnsQuery:RFC8484-5.1-1 | MUST | A client that caches answers accounts for the response's `Age` when it computes TTLs. | n/a: curl caches no answer; it writes each record's TTL as the server sent it |
| DnsQuery:RFC8484-5.2-1 | SHOULD | HTTP/2 is the minimum recommended version of HTTP for DoH. | DnsOverHttpsCases.Https_Request_IsSentOverHttp2 |
| DnsQuery:RFC8484-3-1 | MUST NOT | A client does not use a different URI for DoH simply because it was discovered outside its configuration. | n/a: curl derives its one DoH URI, `https://<authority>/dns-query`, from the dns: URL it is given (ADR-1148) |

## RFC 9250 (DNS over QUIC)

Measured by BL-4682 and BL-4683.
