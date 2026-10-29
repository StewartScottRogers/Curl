# Curl.Extension.Kafka.UnitLibrary

The Kafka extension (ADR-0999, ADR-1076, ADR-1081), tested against the Apache Kafka protocol guide
(<https://kafka.apache.org/protocol>), not against curl. It references only `Curl.Extension.Abstractions` (and through
it `Curl.Protocol.Abstractions`) and the base class library. The handler is registered by one line in
`ExtensionCatalogue.cs` and described in `ExtensionDescriptionCatalogue.cs`.

`Client/` (namespace `Curl.Extension.Kafka.Client`):

- `KafkaExtension` is the description `curl --extensions` lists and the handler factory, over
  `ProtocolHandlerServices.Connector`.
- `KafkaProtocolHandler` serves `kafka://` (port 9092) and `kafkas://` (port 9093, TLS); `KafkaAddress` reads the
  topic, partition and `?offset=N` from the URL.
- `KafkaClientFlow` authenticates with SASL PLAIN when the transfer has `--user` (ADR-1081; the handler does so on every connection), then finds the partition's leader with Metadata (ADR-1080; the handler reconnects to it when it is
  another broker), then produces one record with a body, or fetches from the offset to the high watermark and writes each
  record value and a line feed. `KafkaClientMessages` holds the error texts and `KafkaClientException` carries the exit
  code.

`Wire/` (namespace `Curl.Extension.Kafka.Wire`):

- `KafkaWriter` and `KafkaReader` are the primitive types; `Crc32C` is the record batch checksum.
- `KafkaRecordBatch` writes and reads magic 2 record batches (`KafkaRecord`, `KafkaRecordSet`), gzip included.
- `KafkaRequests` writes Metadata v4, Produce v3 and Fetch v4; `KafkaResponses` reads the Produce and Fetch responses
  into `KafkaPartitionResponse`, and `KafkaMetadataResponse` reads Metadata into the partition's `KafkaMetadataLeader`; `KafkaResponseReader` reads one sized response and checks its correlation id.

`Wire/KafkaSasl/` (namespace `Curl.Extension.Kafka.Wire.KafkaSasl`): `KafkaSaslRequests` writes SaslHandshake v1 and
SaslAuthenticate v0 with the RFC 4616 message, `KafkaSaslResponses` reads their responses into
`KafkaSaslHandshakeResponse` and `KafkaSaslAuthenticateResponse`, and `KafkaSaslMessages` holds the exit 67 texts.
