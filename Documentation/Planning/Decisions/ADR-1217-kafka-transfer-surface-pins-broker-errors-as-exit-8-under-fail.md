# ADR-1217: The Kafka transfer surface pins broker errors as exit 8 under `--fail`

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4630

Decided by Claude under Stewart's delegation.

## Context

BL-4630 checks curl's generic transfer options for `kafka` and `kafkas`
(`Documentation/Conformance/Extended/CurlSurface-Transfer-Brokers-Kafka.md`). Its criteria ask for exit 22 under
`--fail` and `--fail-with-body` "where the library maps a server error to it". The Kafka extension maps none: a broker
error code ends the transfer with exit 8 (CURLE_WEIRD_SERVER_REPLY) and a message naming the code, topic and partition
(ADR-1076), and curl's manual says `--fail` acts on HTTP response codes of 400 or more, which a Kafka exchange has
none of. curl itself ignores `--fail` for a protocol without response codes of that kind.

## Decision

The `--fail` and `--fail-with-body` cases pin exit 8 and `curl: (8) Kafka broker answered error 1
(OFFSET_OUT_OF_RANGE) for topic orders partition 0` for a fetch past the high watermark, with nothing else on standard
output than what `-w` writes, the same as without either option. The extension keeps its exit 8; nothing is changed
to make `--fail` map broker errors to 22.

## Consequences

The transfer surface agrees with ADR-1076, and a change that made `--fail` turn a Kafka broker error into exit 22, or
swallow it, would fail these cases.
