# DnsQuery requirement matrix

Curl's `dns://` client (`Curl.Extended.Protocol.DnsQuery.UnitLibrary`) measured over UDP and TCP against RFC 1035,
RFC 3596, RFC 2782, RFC 8659, RFC 3597, RFC 5452, RFC 7766 and RFC 4501, through the reference DNS server in
`Curl.Conformance.Extended.UnitLibrary/DnsQuery/Reference/` (BL-4514). The encrypted transports (DNS over TLS, QUIC and HTTPS) have their own matrix, [DnsQuery-Encrypted.md](DnsQuery-Encrypted.md) (BL-4474).

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
| DnsQuery:RFC1035-4.1.4-4 | MUST | A name made of labels then a pointer, and a pointer to such a name, are read as the whole name. | DnsQueryMessageTests.Reply_WithAPointerToAPointerAfterALabel_IsReadAsTheWholeName |
| DnsQuery:RFC1035-4.1.4-5 | MUST | A compression pointer to a later offset than its own is refused: exit 8, no output. | DnsQueryMessageTests.Reply_WithAPointerForwardToALaterName_IsRefused |
| DnsQuery:RFC1035-4.1.4-6 | MUST | A compression pointer to itself is refused rather than followed forever: exit 8, no output. | DnsQueryMessageTests.Reply_WithAPointerToItself_IsRefusedRatherThanFollowedForever |
| DnsQuery:RFC5452-9.1-1 | MUST | A UDP reply whose ID is not the query's is ignored and the matching reply after it taken. | DnsQueryMessageTests.ReplyWithAnotherId_IsIgnoredAndTheMatchingReplyAfterItTaken |
| DnsQuery:RFC5452-9.1-2 | MUST | A UDP reply whose question is not the query's is ignored and the matching reply after it taken. | DnsQueryMessageTests.ReplyWithAnotherQuestion_IsIgnoredAndTheMatchingReplyAfterItTaken |
| DnsQuery:RFC1035-4.1.1-3 | MUST | RCODE 0 is written as `status: NOERROR` with the answers, exit 0. | DnsQueryMessageTests.ResponseCode0_NoError_IsWrittenWithItsAnswersAndExit0 |
| DnsQuery:RFC1035-4.1.1-4 | MUST | RCODE 1 is written as `status: FORMERR`, exit 0 as dig does. | DnsQueryMessageTests.ResponseCode1_FormatError_IsWrittenAsFormerrWithExit0 |
| DnsQuery:RFC1035-4.1.1-5 | MUST | RCODE 2 is written as `status: SERVFAIL`, exit 0 as dig does. | DnsQueryMessageTests.ResponseCode2_ServerFailure_IsWrittenAsServfailWithExit0 |
| DnsQuery:RFC1035-4.1.1-6 | MUST | RCODE 3 is written as `status: NXDOMAIN`, exit 0 as dig does. | DnsQueryMessageTests.ResponseCode3_NameError_IsWrittenAsNxdomainWithExit0 |
| DnsQuery:RFC1035-4.1.1-7 | MUST | RCODE 4 is written as `status: NOTIMP`, exit 0 as dig does. | DnsQueryMessageTests.ResponseCode4_NotImplemented_IsWrittenAsNotimpWithExit0 |
| DnsQuery:RFC1035-4.1.1-8 | MUST | RCODE 5 is written as `status: REFUSED`, exit 0 as dig does. | DnsQueryMessageTests.ResponseCode5_Refused_IsWrittenAsRefusedWithExit0 |
| DnsQuery:RFC1035-4.2.1-1 | MUST | A UDP reply with TC set is truncated and is not taken as the answer. | DnsQueryTransportTests.UdpReplyWithTruncationSet_IsAskedAgainOverTcpWithTheLengthPrefix |
| DnsQuery:RFC7766-5-1 | MUST | A client that gets a UDP reply with TC set asks the query again over TCP. | DnsQueryTransportTests.UdpReplyWithTruncationSet_IsAskedAgainOverTcpWithTheLengthPrefix |
| DnsQuery:RFC1035-4.2.2-2 | MUST | The query asked again over TCP is the same message, after its two-octet length prefix. | DnsQueryTransportTests.UdpReplyWithTruncationSet_IsAskedAgainOverTcpWithTheLengthPrefix |
| DnsQuery:RFC1035-7.2-1 | SHOULD | A UDP query that gets no answer in its wait is sent again, with the same ID. | DnsQueryTransportTests.UdpQueryWithNoAnswer_IsSentAgainWithTheSameIdAfterItsWait |
| DnsQuery:RFC1035-7.2-2 | SHOULD | After a bounded number of attempts with no answer the client gives up: exit 28, no output. | DnsQueryTransportTests.UdpQueryNeverAnswered_GivesUpAfterThreeAttemptsWithExit28 |
| DnsQuery:RFC1035-4.2.1-2 | MUST | A message carried by UDP is at most 512 octets. | n/a: a query holds one question whose name is at most 255 octets, so curl's UDP query is always under 512 octets |
| DnsQuery:RFC5452-9.2-1 | SHOULD | The message ID and the UDP source port are chosen at random for each query. | n/a: randomness is not observable from one run's wire bytes; the reference server only sees what one query carries |
| DnsQuery:RFC7766-6.2.1-1 | MUST | A client that pipelines queries on one TCP connection matches each response to its query by ID, in any order. | n/a: curl sends one query per TCP connection and never pipelines |
| DnsQuery:RFC7766-6.2.1.1-1 | SHOULD | A client that pipelines queries handles responses that arrive out of order. | n/a: curl sends one query per TCP connection and never pipelines |
| DnsQuery:RFC7766-8-1 | SHOULD | The two-octet length field and its message are passed to TCP in one write. | n/a: the harness records the bytes curl sent, not how TCP segmented them |
| DnsQuery:RFC7766-6.2.3-1 | SHOULD | A client closes an idle TCP connection it no longer needs. | n/a: curl closes the connection when its one exchange ends, so no connection is left idle |
| DnsQuery:RFC4501-2-5 | MUST | With no `CLASS=` element the query's class is IN, and with no `TYPE=` element its type is A. | DnsQueryTransportTests.Url_WithNoElementsOrTransport_IsQueriedForTypeAClassInOverUdp |
| DnsQuery:RFC4501-2-6 | MUST | With no authority the client asks its configured resolver. | n/a: that is the machine's DNS configuration, which a conformance case never reads; every case names its server |
