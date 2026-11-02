# Kafka: compressed record batches (gzip, Snappy, LZ4, zstd)

Curl's `kafka://` consume of record batches v2 whose records are compressed, measured against the record batch
attributes' codec bits (<https://kafka.apache.org/documentation/#recordbatch>), gzip (RFC 1952), Snappy
(google/snappy `format_description.txt`, and the xerial framing of snappy-java's `SnappyOutputStream` that Kafka's Java
producer writes), LZ4 (lz4/lz4 `doc/lz4_Frame_format.md` and `doc/lz4_Block_format.md`) and zstd (RFC 8878).
Uncompressed batches are in [Kafka-Records.md](Kafka-Records.md). The client produces uncompressed batches only
(`KafkaRecordBatch.Encode` writes attributes 0), so every requirement here is the decoder's.

The cases run against the reference broker's `KafkaFetchApi`, serving a batch written by `KafkaCompressedRecordBatch`
(`Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/`) with an encoder the test owns and shares nothing with the
client library: gzip through the BCL's `GZipStream`; Snappy and LZ4 through `KafkaReferenceSnappyEncoder` and
`KafkaReferenceLz4FrameWriter`, greedy encoders written from the format documents (Snappy with every copy written at
the one offset width a case names); zstd through `KafkaReferenceZstdFrameWriter`, which lays a frame out block by
block as the case says, its compressed block written by hand from RFC 8878 with raw literals and RLE sequence codes
(the case's comment spells out each sequence). Checksums are XXH32 and XXH64 (`KafkaReferenceXxHash`, pinned to the
published vectors).

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Kafka:Batch-Compression-1 | MUST | The attributes' low three bits name the codec: 0 none, 1 gzip, 2 snappy, 3 lz4, 4 zstd. | KafkaGzipTests.Fetch_GzipBatch_WritesEveryRecord |
| Kafka:Batch-Compression-2 | MUST | The header through the record count stays uncompressed; the records after it are one compressed payload, which the CRC-32C covers as sent. | KafkaGzipTests.Fetch_GzipBatch_WritesEveryRecord |
| Kafka:Batch-Compression-3 | MUST | A payload the codec cannot decode is corrupt and none of its records is delivered. | KafkaGzipTests.Fetch_CorruptGzip_FailsWithWeirdServerReply |
| Kafka:Batch-Compression-4 | MUST | A producer compresses with the codec it names in the attributes. | n/a: the client produces uncompressed batches only |
| Kafka:RFC1952-2.3-1 | MUST | A gzip member (ID1 ID2, CM 8, deflate data, CRC32 and ISIZE) decodes to the records. | KafkaGzipTests.Fetch_GzipBatch_WritesEveryRecord |
| Kafka:RFC1952-2.3-2 | MUST | Input that is not a gzip member is an error. | KafkaGzipTests.Fetch_CorruptGzip_FailsWithWeirdServerReply |
| Kafka:Snappy-Format-1 | MUST | Raw Snappy starts with the uncompressed length as a little-endian base-128 varint. | KafkaSnappyTests.Fetch_RawSnappyWithOneByteOffsetCopiesAndLongLiterals_WritesEveryRecord |
| Kafka:Snappy-Format-2 | MUST | A literal (tag 00) holds its length less one in the tag's upper six bits below 60, or in the 1 to 4 bytes after it for 60 to 63. | KafkaSnappyTests.Fetch_RawSnappyWithOneByteOffsetCopiesAndLongLiterals_WritesEveryRecord |
| Kafka:Snappy-Format-3 | MUST | A copy with a one-byte offset (tag 01) has length 4 to 11 and an 11-bit offset, three bits in the tag. | KafkaSnappyTests.Fetch_RawSnappyWithOneByteOffsetCopiesAndLongLiterals_WritesEveryRecord |
| Kafka:Snappy-Format-4 | MUST | A copy with a two-byte offset (tag 10) has length 1 to 64 and a little-endian 16-bit offset. | KafkaSnappyTests.Fetch_TwoByteOffsetCopiesPast2048Bytes_WritesTheRecord |
| Kafka:Snappy-Format-5 | MUST | A copy with a four-byte offset (tag 11) has length 1 to 64 and a little-endian 32-bit offset. | KafkaSnappyTests.Fetch_FourByteOffsetCopiesPast65535Bytes_WritesTheRecord |
| Kafka:Snappy-Format-6 | MUST | A copy whose offset is less than its length repeats the bytes it is writing. | KafkaSnappyTests.Fetch_TwoByteOffsetCopiesPast2048Bytes_WritesTheRecord |
| Kafka:Snappy-Format-7 | MUST | Output that does not reach the declared length is corrupt. | KafkaSnappyTests.Fetch_SnappyShorterThanItsDeclaredLength_FailsWithWeirdServerReply |
| Kafka:Snappy-Format-8 | MUST | A copy offset of zero, or one before the start of the output, is corrupt. | KafkaSnappyTests.Fetch_SnappyCopyWithOffsetZero_FailsWithWeirdServerReply |
| Kafka:Snappy-Xerial-1 | MUST | Xerial framing starts with the magic 0x82 "SNAPPY" 0x00, a version and a compatible version, each a big-endian INT32. | KafkaSnappyTests.Fetch_XerialFramingOfTwoChunks_WritesEveryRecord |
| Kafka:Snappy-Xerial-2 | MUST | Xerial chunks are each a big-endian INT32 length and that many bytes of raw Snappy, decoded one after another. | KafkaSnappyTests.Fetch_XerialFramingOfTwoChunks_WritesEveryRecord |
| Kafka:LZ4-Frame-1 | MUST | A frame starts with the magic 0x184D2204, little-endian. | KafkaLz4Tests.Fetch_Lz4FrameOfOneBlock_WritesEveryRecord |
| Kafka:LZ4-Frame-2 | MUST | The descriptor's FLG holds version 01 and its flags, BD the block maximum size, and HC the header checksum byte. | KafkaLz4Tests.Fetch_Lz4FrameOfOneBlock_WritesEveryRecord |
| Kafka:LZ4-Frame-3 | MUST | The optional content size (8 bytes) and dictionary id (4 bytes) follow BD when their flags are set. | n/a: Kafka's producer (KafkaLZ4BlockOutputStream) sets neither flag, and the reference writer has no case for them |
| Kafka:LZ4-Frame-4 | MUST | A block size with its highest bit set holds the block stored uncompressed. | KafkaLz4Tests.Fetch_StoredBlocks_WritesEveryRecord |
| Kafka:LZ4-Frame-5 | MUST | A block size of zero is the end mark that ends the frame. | KafkaLz4Tests.Fetch_Lz4FrameOfOneBlock_WritesEveryRecord |
| Kafka:LZ4-Frame-6 | MUST | With the block checksum flag, every block is followed by a 4-byte XXH32 of its stored bytes. | KafkaLz4Tests.Fetch_LinkedBlocksWithBlockAndContentChecksums_WritesEveryRecord |
| Kafka:LZ4-Frame-7 | MUST | With the content checksum flag, the end mark is followed by a 4-byte XXH32 of the whole content. | KafkaLz4Tests.Fetch_LinkedBlocksWithBlockAndContentChecksums_WritesEveryRecord |
| Kafka:LZ4-Frame-8 | SHOULD | A decoder checks the header, block and content checksums. | n/a: the client skips them (Lz4FrameDecompressor), as the batch's CRC-32C already covers the compressed bytes; a case cannot tell a skipped checksum from a checked one without a corrupt checksum under a good CRC, which the format leaves optional |
| Kafka:LZ4-Frame-9 | MUST | With the block independence flag clear, a match may copy from earlier blocks. | KafkaLz4Tests.Fetch_LinkedBlocksWithBlockAndContentChecksums_WritesEveryRecord |
| Kafka:LZ4-Frame-11 | MUST | Input without the magic is not a frame. | KafkaLz4Tests.Fetch_Lz4WithAWrongMagic_FailsWithWeirdServerReply |
| Kafka:LZ4-Block-1 | MUST | A sequence is a token (literal length and match length less 4, a nibble each), the literals, a little-endian 16-bit offset and the match. | KafkaLz4Tests.Fetch_Lz4FrameOfOneBlock_WritesEveryRecord |
| Kafka:LZ4-Block-2 | MUST | A nibble of 15 is extended by bytes added to it until one is below 255. | KafkaLz4Tests.Fetch_LongLiteralsAndLongOverlappingMatch_WritesTheRecord |
| Kafka:LZ4-Block-3 | MUST | A match whose offset is less than its length repeats the bytes it is writing. | KafkaLz4Tests.Fetch_LongLiteralsAndLongOverlappingMatch_WritesTheRecord |
| Kafka:LZ4-Block-4 | MUST | The last sequence holds literals only and ends the block. | KafkaLz4Tests.Fetch_Lz4FrameOfOneBlock_WritesEveryRecord |
| Kafka:LZ4-Block-5 | MUST | An offset of zero, or one before the start of the output, is corrupt. | KafkaLz4Tests.Fetch_Lz4MatchWithOffsetZero_FailsWithWeirdServerReply |
| Kafka:RFC8878-3.1.1-1 | MUST | A zstd frame starts with the magic 0xFD2FB528, little-endian. | KafkaZstdTests.Fetch_ZstdFrameOfOneRawBlock_WritesEveryRecord |
| Kafka:RFC8878-3.1.1-2 | MUST | Input that starts with neither the frame magic nor a skippable frame's is corrupt. | KafkaZstdTests.Fetch_ZstdWithAWrongMagic_FailsWithWeirdServerReply |
| Kafka:RFC8878-3.1.1-3 | MUST | With Content_Checksum_Flag set, the 4 bytes after the last block are the low 32 bits of the content's XXH64. | KafkaZstdTests.Fetch_SkippableFrameThenFrameWithContentChecksum_WritesEveryRecord |
| Kafka:RFC8878-3.1.1-4 | SHOULD | A decoder checks the content checksum. | n/a: the client skips it (ZstdFrameDecompressor), as the batch's CRC-32C already covers the compressed bytes |
| Kafka:RFC8878-3.1.1.1-1 | MUST | The frame header descriptor gives Single_Segment_Flag, Frame_Content_Size and the content size the blocks must add up to. | KafkaZstdTests.Fetch_ZstdFrameOfOneRawBlock_WritesEveryRecord |
| Kafka:RFC8878-3.1.1.1-2 | MUST | A frame naming a dictionary the decoder does not hold cannot be decoded. | KafkaZstdTests.Fetch_FrameNamingADictionary_FailsWithWeirdServerReply |
| Kafka:RFC8878-3.1.1.2-1 | MUST | A Raw_Block's content is copied to the output as it is. | KafkaZstdTests.Fetch_ZstdFrameOfOneRawBlock_WritesEveryRecord |
| Kafka:RFC8878-3.1.1.2-2 | MUST | An RLE_Block is one byte repeated Block_Size times. | KafkaZstdTests.Fetch_RleBlockBetweenRawBlocks_WritesTheRecord |
| Kafka:RFC8878-3.1.1.2-3 | MUST | A Compressed_Block is a literals section and a sequences section. | KafkaZstdTests.Fetch_CompressedBlockWithRepeatOffsets_WritesTheRecord |
| Kafka:RFC8878-3.1.1.2-4 | MUST | Blocks follow one another until the one with Last_Block set. | KafkaZstdTests.Fetch_RleBlockBetweenRawBlocks_WritesTheRecord |
| Kafka:RFC8878-3.1.1.3.1-1 | MUST | A Raw_Literals_Block holds the literals as they are, its size in the literals section header. | KafkaZstdTests.Fetch_CompressedBlockWithRepeatOffsets_WritesTheRecord |
| Kafka:RFC8878-3.1.1.3.1-2 | MUST | Compressed_Literals_Block and Treeless_Literals_Block hold Huffman-coded literals in one or four streams, with the tree described or reused. | n/a: the reference has no Huffman encoder yet; the cases are BL-4572's |
| Kafka:RFC8878-3.1.1.3.2-1 | MUST | The sequences section header gives Number_of_Sequences and Symbol_Compression_Modes; RLE_Mode gives each code as one byte. | KafkaZstdTests.Fetch_CompressedBlockWithRepeatOffsets_WritesTheRecord |
| Kafka:RFC8878-3.1.1.3.2-2 | MUST | Predefined_Mode, FSE_Compressed_Mode and Repeat_Mode decode each code with the default, described or previous FSE table. | n/a: the reference has no FSE encoder yet; the cases are BL-4572's |
| Kafka:RFC8878-3.1.1.4-1 | MUST | Each sequence copies its literals, then its match, and the literals left after the last sequence end the block. | KafkaZstdTests.Fetch_CompressedBlockWithRepeatOffsets_WritesTheRecord |
| Kafka:RFC8878-3.1.1.5-1 | MUST | An offset value of 1 to 3 names a repeat offset, starting from 1, 4 and 8, and using one moves it to the front. | KafkaZstdTests.Fetch_CompressedBlockWithRepeatOffsets_WritesTheRecord |
| Kafka:RFC8878-3.1.2-1 | MUST | A skippable frame (magic 0x184D2A5?, a 4-byte size and that many bytes) is skipped. | KafkaZstdTests.Fetch_SkippableFrameThenFrameWithContentChecksum_WritesEveryRecord |
