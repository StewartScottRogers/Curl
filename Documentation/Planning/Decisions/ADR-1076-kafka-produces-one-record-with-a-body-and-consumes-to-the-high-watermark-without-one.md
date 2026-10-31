# ADR-1076: Kafka produces one record with a body and consumes to the high watermark without one

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4121)

## Context

BL-4121 adds Kafka as an extension (ADR-0999), tested against the Apache Kafka protocol guide
(https://kafka.apache.org/protocol), not against curl, which has no Kafka. It needs scheme names, ports, a URL form,
the protocol versions to speak, a mapping from curl's options to requests, and exit codes for the failures.

## Decision

- Schemes `kafka` (default port 9092, the broker's conventional plaintext port) and `kafkas` (default port 9093, the
  conventional TLS listener port; Kafka registers no URL scheme), TLS from curl's connector, so `-k`, `--cacert` and
  the other TLS options apply. The library is `Curl.Extension.Kafka.UnitLibrary`; it adds no command-line option.
- URL form `kafka://host[:port]/topic[/partition][?offset=N]`: the first path segment, URL-decoded, is the topic; the
  second, a decimal number, the partition (default 0); the query `offset=N` the offset a consume starts at (default
  0). No topic, a third segment, a partition or query in any other form ends with exit 3 before connecting.
- Versions: Produce v3 and Fetch v4 with request header v1 and response header v0. They are the oldest versions Kafka
  4.0 still accepts (KIP-896 removed Produce v0 to v2 and Fetch v0 to v3) and the newest before flexible versions, so
  one non-flexible encoder serves every broker from 0.11 on. ApiVersions is not sent: every broker that supports
  these versions answers them. Records are written and read as record batches with magic 2, with a CRC-32C written
  by hand (the BCL has none).
- With a body (`-d` and friends), the flow sends one Produce of one record (no key, no headers, the body as its value,
  the current time from the injected `TimeProvider` as its timestamp) with `acks=-1` and a 30 s timeout, and ends
  when the partition answers. It writes nothing to the output, as an upload does not.
- Without a body, the flow sends Fetch from the offset (`max_wait_ms` 500, `min_bytes` 1, 1 MiB limits,
  `isolation_level` 0, read uncommitted), writes each record's value followed by a line feed (kcat's default record
  delimiter) and fetches again from after the last whole batch until it reaches the partition's high watermark. A
  fetch that makes no progress ends the transfer, so it never loops. Records before the offset in the first batch are
  skipped, control batches are skipped, a partial batch at the end of a fetch is ignored (the guide allows the
  broker to send one), a null value is written as an empty line, and gzip batches are read with `GZipStream`
  (snappy, lz4 and zstd: see "Left out").
- Exit codes: a partition error code is 8 (`Kafka broker answered error <code> (<NAME>) for topic <t> partition <p>`);
  a malformed response, a failed CRC-32C, a wrong correlation id or a size outside 4 bytes to 64 MiB is 8; the
  connection ending inside or before a response, or a failed read, is 56; a failed write to the broker is 55; a
  failed write to the output is 23.

## Left out, and why

- Snappy, lz4 and zstd batches: no longer left out. BL-4277 reads them with decoders written by hand under the
  extension's `Wire/Compression` (raw and xerial-framed snappy, the LZ4 frame format, zstd frames per RFC 8878); a
  corrupt one ends with exit 8 and a message naming the codec.
- Metadata and leader routing: superseded by ADR-1080 (BL-4275), which asks the URL's broker for Metadata v4 first
  and sends Produce or Fetch to the partition's leader.
- SASL authentication (`--user`): superseded by ADR-1082 (BL-4276), which authenticates every connection with SASL
  PLAIN before its first request.
- Keys, headers, transactions, idempotent producing, consumer groups and offset commits: no curl option maps onto
  them without inventing new options. A later task can add options for them through the extension option catalogue.
- Read committed isolation: it needs the aborted transactions list applied to the batches; read uncommitted is
  what a plain consumer sees.

## Consequences

`curl kafka://broker/orders -d hi` produces one record and `curl 'kafka://broker/orders/0?offset=5'` writes every
record from offset 5 to the end of the partition, one per line, with `CURL_EXTENSIONS=0` failing both as curl fails
an unsupported scheme (exit 1).
