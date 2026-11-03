# Kafka: framing, Metadata and SASL PLAIN

Curl's `kafka://` and `kafkas://` client measured against the Apache Kafka protocol guide
(<https://kafka.apache.org/protocol>): the size-prefixed framing, request header v1, the primitive types, Metadata v4,
SaslHandshake v1 and SaslAuthenticate v0 with the RFC 4616 PLAIN message, and the error codes table. The guide has few
RFC-style keywords, so each "must", "should" and each schema field the client writes or relies on is a requirement.
Produce, Fetch and record batches are in [Kafka-Records.md](Kafka-Records.md) (BL-4480); compression is in
[Kafka-Compression.md](Kafka-Compression.md) (BL-4481).

The cases run against `KafkaReferenceBroker` (`Curl.Conformance.Extended.UnitLibrary/Kafka/`), which serves the APIs
it is given; this part gives it Metadata and the two SASL APIs, so a transfer that reaches its Fetch ends when the
broker closes the connection on an API it does not serve (exit 56).

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Kafka:Guide-Framing-1 | MUST | A request is an INT32 size followed by that many bytes of request header and body. | KafkaFramingTests.Request_MetadataFirst_IsSizePrefixedWithHeaderV1 |
| Kafka:Guide-Framing-2 | MUST | The client matches a response to its request by correlation id; a response carrying another id is not its answer. | KafkaFramingTests.Response_WrongCorrelationId_FailsWithWeirdServerReply |
| Kafka:Guide-Framing-3 | MUST | A response is an INT32 size followed by that many bytes; one cut short is a receive failure, not a reply. | KafkaFramingTests.Response_TruncatedBeforeItsSize_FailsWithRecvError |
| Kafka:Guide-Header-1 | MUST | Request header v1 is api_key INT16, api_version INT16, correlation_id INT32, client_id NULLABLE_STRING. | KafkaFramingTests.Request_MetadataFirst_IsSizePrefixedWithHeaderV1 |
| Kafka:Guide-Header-2 | SHOULD | Each request on a connection carries its own correlation id. | KafkaSaslTests.Sasl_PlainOffered_AuthenticatesBeforeMetadata |
| Kafka:Guide-Types-1 | MUST | INT8, INT16 and INT32 are big-endian; a BOOLEAN is an INT8 of 0 or 1. | KafkaFramingTests.Request_MetadataFirst_IsSizePrefixedWithHeaderV1 |
| Kafka:Guide-Types-2 | MUST | STRING is an INT16 length N and N UTF-8 bytes. | KafkaFramingTests.Request_MetadataFirst_IsSizePrefixedWithHeaderV1 |
| Kafka:Guide-Types-3 | MUST | ARRAY is an INT32 count N and N elements. | KafkaFramingTests.Request_MetadataFirst_IsSizePrefixedWithHeaderV1 |
| Kafka:Guide-Types-4 | MUST | BYTES is an INT32 length N and N bytes. | KafkaFramingTests.Request_MetadataFirst_IsSizePrefixedWithHeaderV1 |
| Kafka:Guide-Types-5 | MUST | A NULLABLE_STRING of length -1 is null (Metadata's rack and cluster_id). | KafkaMetadataTests.Metadata_LeaderIsThisBroker_FetchesOverTheSameConnection |
| Kafka:Guide-Types-6 | MUST | INT64, VARINT and VARLONG. | n/a: only Produce, Fetch and record batches use them, measured in Kafka-Records.md |
| Kafka:Guide-Types-7 | MUST | The compact (flexible version) types and tagged fields. | n/a: the client sends no flexible version (Metadata v4, SaslHandshake v1, SaslAuthenticate v0) |
| Kafka:Guide-Metadata-1 | MUST | Metadata v4 names the topics and sends allow_auto_topic_creation. | KafkaFramingTests.Request_MetadataFirst_IsSizePrefixedWithHeaderV1 |
| Kafka:Guide-Metadata-2 | MUST | Requests for a partition go to its leader; when that is this broker, the connection is reused. | KafkaMetadataTests.Metadata_LeaderIsThisBroker_FetchesOverTheSameConnection |
| Kafka:Guide-Metadata-3 | MUST | When the leader is another broker, the client connects to the host and port Metadata lists for its node id. | KafkaMetadataTests.Metadata_LeaderIsAnotherBroker_ReconnectsToItsHostAndPort |
| Kafka:Guide-Metadata-4 | MUST | A topic error_code is an error; the client does not go on to the partition. | KafkaMetadataTests.Metadata_UnknownTopic_FailsNamingTheErrorCode |
| Kafka:Guide-Metadata-5 | MUST | A partition error_code (LEADER_NOT_AVAILABLE, leader -1) is an error. | KafkaMetadataTests.Metadata_PartitionLeaderNotAvailable_FailsNamingTheErrorCode |
| Kafka:Guide-Errors-1 | SHOULD | Error codes are reported by their name in the error codes table. | KafkaMetadataTests.Metadata_UnknownTopic_FailsNamingTheErrorCode |
| Kafka:Guide-Sasl-1 | MUST | With SASL, SaslHandshake (mechanism PLAIN) is the first request on every connection. | KafkaSaslTests.Sasl_PlainOffered_AuthenticatesBeforeMetadata |
| Kafka:Guide-Sasl-2 | MUST | A handshake answered UNSUPPORTED_SASL_MECHANISM ends the attempt; the enabled mechanisms are reported. | KafkaSaslTests.Sasl_PlainNotOffered_FailsWithLoginDenied |
| Kafka:Guide-Sasl-3 | MUST | SaslAuthenticate v0 carries the RFC 4616 PLAIN message, authzid NUL authcid NUL passwd, as BYTES. | KafkaSaslTests.Sasl_PlainOffered_AuthenticatesBeforeMetadata |
| Kafka:Guide-Sasl-4 | MUST | A SaslAuthenticate error_code ends the connection's authentication with its error_message (exit 67). | KafkaSaslTests.Sasl_AuthenticateRefused_FailsWithLoginDenied |
| Kafka:Guide-Tls-1 | MUST | `kafkas://` speaks the same protocol over TLS (port 9093). | KafkaMetadataTests.Kafkas_TlsListener_CarriesTheSameRequests |
