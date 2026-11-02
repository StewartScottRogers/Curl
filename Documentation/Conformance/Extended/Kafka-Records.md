# Kafka: Produce v3, Fetch v4 and record batch v2

Curl's `kafka://` produce (`-d`) and consume measured against the Apache Kafka protocol guide
(<https://kafka.apache.org/protocol>): Produce v3 and Fetch v4, and the "Record Batch" and "Record" definitions
(<https://kafka.apache.org/documentation/#recordbatch>), uncompressed. Compressed batches are in
[Kafka-Compression.md](Kafka-Compression.md) (BL-4481); framing, Metadata and SASL are in [Kafka.md](Kafka.md).

The cases run against `KafkaReferenceBroker` given `KafkaProduceApi` and `KafkaFetchApi`
(`Curl.Conformance.Extended.UnitLibrary/Kafka/Records/`), which store batches in a `KafkaReferenceLog` and write and
check them with their own codec and CRC-32C, sharing nothing with the client library.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Kafka:Guide-Types-8 | MUST | INT64 is big-endian; VARINT and VARLONG are zig-zag encoded base-128 varints. | KafkaProduceTests.Produce_OneValue_SendsABatchTheBrokerVerifiesFieldByFieldAndByCrc32C |
| Kafka:Guide-Produce-1 | MUST | Produce v3 sends transactional_id (null outside a transaction), acks, timeout_ms and, per partition, the records as BYTES. | KafkaProduceTests.Produce_OneValue_SendsABatchTheBrokerVerifiesFieldByFieldAndByCrc32C |
| Kafka:Guide-Produce-2 | MUST | A Produce response's partition error_code 0 with its base_offset is success. | KafkaProduceTests.Produce_OneValue_SendsABatchTheBrokerVerifiesFieldByFieldAndByCrc32C |
| Kafka:Guide-Produce-3 | MUST | A Produce response's partition error_code is an error reported by its name. | KafkaProduceTests.Produce_PartitionErrorCode_FailsNamingTheErrorCode |
| Kafka:Guide-Produce-4 | MUST | With acks 0 the broker sends no response. | n/a: the client always sends acks -1 and waits for the response |
| Kafka:Guide-Fetch-1 | MUST | Fetch v4 sends replica_id -1, max_wait_ms, min_bytes, max_bytes, isolation_level and, per partition, fetch_offset and partition max_bytes. | KafkaFetchTests.Fetch_SeveralBatches_WritesEveryRecordToTheHighWatermark |
| Kafka:Guide-Fetch-2 | MUST | The client reads high_watermark, last_stable_offset and the aborted_transactions array before the records. | KafkaFetchTests.Fetch_SeveralBatches_WritesEveryRecordToTheHighWatermark |
| Kafka:Guide-Fetch-3 | MUST | A response's records hold several batches, read one after another up to the high watermark. | KafkaFetchTests.Fetch_SeveralBatches_WritesEveryRecordToTheHighWatermark |
| Kafka:Guide-Fetch-4 | MUST | The broker answers from the start of the batch holding fetch_offset; records before it are skipped by the client. | KafkaFetchTests.Fetch_OffsetInsideABatch_SkipsTheRecordsBeforeIt |
| Kafka:Guide-Fetch-5 | MUST | A partial batch may end a response; the client ignores it and fetches again from its base offset. | KafkaFetchTests.Fetch_PartialBatchAtTheEnd_IsFetchedAgainFromItsBaseOffset |
| Kafka:Guide-Fetch-6 | MUST | An empty partition (high watermark equal to the fetch offset) answers no records. | KafkaFetchTests.Fetch_EmptyPartition_WritesNothingAndSucceeds |
| Kafka:Guide-Fetch-7 | MUST | A partition error_code (OFFSET_OUT_OF_RANGE) is an error reported by its name. | KafkaFetchTests.Fetch_OffsetOutOfRange_FailsNamingTheErrorCode |
| Kafka:Guide-Fetch-8 | MUST | Under READ_COMMITTED the client drops records of the aborted transactions listed. | n/a: the client fetches with isolation_level 0 (READ_UNCOMMITTED), so aborted transactions are delivered as the guide defines |
| Kafka:Batch-Header-1 | MUST | A batch is baseOffset INT64, batchLength INT32, partitionLeaderEpoch INT32, magic INT8 (2), crc UINT32, attributes INT16, lastOffsetDelta INT32, baseTimestamp, maxTimestamp, producerId INT64, producerEpoch INT16, baseSequence INT32 and the records count INT32. | KafkaProduceTests.Produce_OneValue_SendsABatchTheBrokerVerifiesFieldByFieldAndByCrc32C |
| Kafka:Batch-Header-2 | MUST | A producer outside idempotence and transactions writes producerId -1, producerEpoch -1 and baseSequence -1. | KafkaProduceTests.Produce_OneValue_SendsABatchTheBrokerVerifiesFieldByFieldAndByCrc32C |
| Kafka:Batch-Header-3 | MUST | batchLength counts the bytes after itself, so the client finds the next batch from it. | KafkaFetchTests.Fetch_SeveralBatches_WritesEveryRecordToTheHighWatermark |
| Kafka:Batch-Crc-1 | MUST | crc is the CRC-32C (Castagnoli) of the bytes from attributes to the end of the batch. | KafkaProduceTests.Produce_OneValue_SendsABatchTheBrokerVerifiesFieldByFieldAndByCrc32C |
| Kafka:Batch-Crc-2 | MUST | A fetched batch whose crc does not match is corrupt and not delivered. | KafkaFetchTests.Fetch_BatchWithABadCrc_FailsWithWeirdServerReply |
| Kafka:Batch-Control-1 | MUST | A batch with the control attribute (bit 5) holds control records, which are not delivered to the application. | KafkaFetchTests.Fetch_ControlBatch_WritesNoRecordOfIt |
| Kafka:Batch-Timestamp-1 | MUST | With the timestamp type attribute (bit 3) set, maxTimestamp is the log append time. | n/a: the client delivers record values only, never timestamps |
| Kafka:Batch-Record-1 | MUST | A record is length VARINT, attributes INT8, timestampDelta VARLONG, offsetDelta VARINT, key and value each a VARINT length (-1 for null) and bytes, and a VARINT header count. | KafkaProduceTests.Produce_OneValue_SendsABatchTheBrokerVerifiesFieldByFieldAndByCrc32C |
| Kafka:Batch-Record-2 | MUST | A null key (length -1) and a null value are read as null. | KafkaFetchTests.Fetch_NullKeysHeadersAndNegativeTimestampDeltas_WritesEachValue |
| Kafka:Batch-Record-3 | MUST | Headers are a VARINT count of a VARINT-length key and a VARINT-length value (-1 for null). | KafkaFetchTests.Fetch_NullKeysHeadersAndNegativeTimestampDeltas_WritesEachValue |
| Kafka:Batch-Record-4 | MUST | timestampDelta is signed, so a record may be older than the batch's base timestamp. | KafkaFetchTests.Fetch_NullKeysHeadersAndNegativeTimestampDeltas_WritesEachValue |
