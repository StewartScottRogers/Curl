# DnsQuery requirement matrix

Curl's `dns://` client (`Curl.Extended.Protocol.DnsQuery.UnitLibrary`) measured over UDP and TCP against RFC 1035,
RFC 3596, RFC 2782, RFC 8659, RFC 3597 and RFC 4501, through the reference DNS server in
`Curl.Conformance.Extended.UnitLibrary/DnsQuery/Reference/` (BL-4514). The encrypted transports are BL-4474's.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| DnsQuery:RFC1035-4.1.1-1 | MUST | A query carries a header with an ID, RD as asked, QDCOUNT 1 and the other counts 0, then the question. | DnsQueryRecordTypeTests.A_IsQueriedAndItsAddressWritten |
| DnsQuery:RFC1035-4.1.1-2 | MUST | The reply header's ID and flag bits (QR, AA, RD, RA) are read as the server set them. | DnsQueryRecordTypeTests.ReplyHeader_IdAndFlagsAreWrittenAsTheServerSetThem |
| DnsQuery:RFC1035-4.1.4-1 | MUST | A name given as a compression pointer to an earlier name is read as that name. | DnsQueryRecordTypeTests.A_IsQueriedAndItsAddressWritten |
| DnsQuery:RFC1035-4.1.4-2 | MUST | A record whose owner is not the question's name, written in full, is read beside compressed ones. | DnsQueryRecordTypeTests.Cname_IsWrittenWithTheRecordsOfItsTargetNamedInFull |
| DnsQuery:RFC1035-4.1.4-3 | MUST | A reply with no compression at all is read (compression is optional for the sender). | DnsQueryRecordTypeTests.Reply_WithEveryNameUncompressed_IsRead |
| DnsQuery:RFC1035-3.4.1-1 | MUST | An A record's RDATA is a 32-bit Internet address. | DnsQueryRecordTypeTests.A_IsQueriedAndItsAddressWritten |
| DnsQuery:RFC3596-2.2-1 | MUST | An AAAA record's RDATA is a 128-bit IPv6 address. | DnsQueryRecordTypeTests.Aaaa_IsQueriedAndItsAddressWritten |
| DnsQuery:RFC1035-3.3.11-1 | MUST | An NS record's RDATA is a domain name, NSDNAME. | DnsQueryRecordTypeTests.Ns_IsQueriedAndItsNameServerWritten |
| DnsQuery:RFC1035-3.3.1-1 | MUST | A CNAME record's RDATA is a domain name, CNAME. | DnsQueryRecordTypeTests.Cname_IsWrittenWithTheRecordsOfItsTargetNamedInFull |
| DnsQuery:RFC1035-3.3.12-1 | MUST | A PTR record's RDATA is a domain name, PTRDNAME. | DnsQueryRecordTypeTests.Ptr_IsQueriedAndItsNameWritten |
| DnsQuery:RFC1035-3.3.9-1 | MUST | An MX record's RDATA is a 16-bit PREFERENCE then the EXCHANGE domain name. | DnsQueryRecordTypeTests.Mx_IsQueriedAndItsPreferenceAndExchangeWritten |
| DnsQuery:RFC1035-3.3.14-1 | MUST | A TXT record's RDATA is one or more character-strings. | DnsQueryRecordTypeTests.Txt_IsQueriedAndEachCharacterStringWritten |
| DnsQuery:RFC1035-3.3.13-1 | MUST | An SOA record's RDATA is MNAME, RNAME and the 32-bit SERIAL, REFRESH, RETRY, EXPIRE and MINIMUM. | DnsQueryRecordTypeTests.Soa_IsQueriedAndItsSevenFieldsWritten |
| DnsQuery:RFC2782-RDATA-1 | MUST | An SRV record's RDATA is Priority, Weight and Port, 16 bits each, then the Target name. | DnsQueryRecordTypeTests.Srv_IsQueriedAndItsPriorityWeightPortAndTargetWritten |
| DnsQuery:RFC8659-4.1-1 | MUST | A CAA record's RDATA is the Flags octet, the Tag with its length octet, then the Value to the end of the RDATA. | DnsQueryRecordTypeTests.Caa_IsQueriedAndItsFlagsTagAndValueWritten |
| DnsQuery:RFC3597-5-1 | MUST | A record of a type the client does not know is written as `\# <length> <hex>`, and the type as `TYPE<n>`. | DnsQueryRecordTypeTests.UnknownType_IsQueriedByItsNumberAndItsRdataWrittenAsGenericHex |
| DnsQuery:RFC1035-4.2.2-1 | MUST | Over TCP the message is prefixed with a two-octet length field. | DnsQueryUrlTests.Authority_OverTcp_IsQueriedWithATwoOctetLengthPrefix |
| DnsQuery:RFC4501-2-1 | MUST | The URI's `TYPE=` element, matched without regard to case, sets the query's QTYPE. | DnsQueryUrlTests.TypeElement_InEitherCase_SetsTheQtype |
| DnsQuery:RFC4501-2-2 | MUST | The URI's `CLASS=` element sets the query's QCLASS; elements are separated by `;`. | DnsQueryUrlTests.ClassElement_WithType_SetsTheQclassAndQtype |
| DnsQuery:RFC4501-2-3 | MUST | The URI's authority names the server queried, and its port the port. | DnsQueryUrlTests.Authority_WithAPort_IsTheServerQueried |
| DnsQuery:RFC4501-2-4 | MUST | The authority's server and port hold whichever transport carries the query. | DnsQueryUrlTests.Authority_OverTcp_IsQueriedWithATwoOctetLengthPrefix |
