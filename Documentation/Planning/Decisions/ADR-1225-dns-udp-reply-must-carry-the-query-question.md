# ADR-1225: A `dns://` reply over UDP must carry the query's question

- Status: Accepted
- Date: 2026-11-03
- Task: BL-4673

Decided by Claude under Stewart's delegation.

## Context

`DnsUdpTransport` took the first datagram that was a response with the query's message ID. RFC 5452 section 9.1
says a resolver accepts a response only when its question section matches the query's: the same QNAME, compared
without regard to ASCII case, the same QTYPE and the same QCLASS. The extension conformance case
`DnsQuery:RFC5452-9.1-2` sends a reply with the right ID but the question `example.net. IN A`, then the right reply.

## Decision

Over UDP, a response with the query's ID is taken only when its first question is the query's question (QNAME
compared case-insensitively in ASCII, QTYPE and QCLASS byte for byte). Any other such datagram is ignored and the
transport keeps waiting within the attempt's time, as for a datagram with another ID. A response whose question
count is zero is still taken: dig checks the question only when the response has one, and servers answer FORMERR
and some other errors without a question section.

## Consequences

An off-path forger now has to guess the question as well as the ID, and a stray reply to an earlier query no longer
ends the transfer. TCP, TLS, QUIC and HTTPS transports are unchanged: their connection already binds the reply to the
query.
