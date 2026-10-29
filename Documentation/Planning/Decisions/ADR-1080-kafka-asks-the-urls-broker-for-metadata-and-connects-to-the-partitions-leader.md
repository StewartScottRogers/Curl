# ADR-1080: Kafka asks the URL's broker for Metadata and connects to the partition's leader

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4275)
- Supersedes the "Metadata and leader routing" item of ADR-1076's "Left out"

## Context

ADR-1076 sent Produce and Fetch to the URL's broker, which answers NOT_LEADER_OR_FOLLOWER (6) when another broker of
the cluster leads the partition. Any broker answers Metadata for the whole cluster, so the client can find the leader
from the broker the URL names, as every Kafka client does with its bootstrap servers.

## Decision

- Before Produce or Fetch the flow sends Metadata v4 for the URL's topic on the URL's connection. Kafka 4.0 removed
  Metadata v0 to v3 (KIP-896), so v4 is the oldest version it accepts, and it is the newest before flexible versions
  (v9), so the one non-flexible encoder of ADR-1076 serves it.
- `allow_auto_topic_creation` is false: a transfer never creates a topic, so a mistyped topic fails rather than
  leaving a topic behind on brokers configured to create topics on demand.
- A topic error code in the response ends the transfer with exit 8 and the message ADR-1076 gives a partition error
  (`Kafka broker answered error 3 (UNKNOWN_TOPIC_OR_PARTITION) for topic <t> partition <p>`). A partition the topic
  does not hold is UNKNOWN_TOPIC_OR_PARTITION (3). A partition whose leader is not among the response's brokers ends
  with its partition error code, or LEADER_NOT_AVAILABLE (5) when it has none. A partition whose leader is known is
  used even with a partition error code, as REPLICA_NOT_AVAILABLE (9) is reported beside a working leader.
- When the leader's host (compared without case) and port are the URL broker's, the flow goes on over the same
  connection. Otherwise the handler closes it and connects through the connector to the leader's advertised host and
  port, with the URL's TLS choice and the transfer's proxy and logs, and sends Produce or Fetch there. It does not
  follow a second redirect: the leader Metadata names is used as it is, and an error from it ends the transfer.

## Consequences

`curl kafka://any-broker/orders -d hi` produces to the partition's leader wherever it is in the cluster, at the cost
of one Metadata round trip on every transfer. A broker whose advertised listener differs from the URL's host name is
reached by its advertised name, which must resolve from the client, as for every Kafka client.
