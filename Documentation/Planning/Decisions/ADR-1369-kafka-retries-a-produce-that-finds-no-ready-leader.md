# ADR-1369: Kafka sends a produce again when the broker has no ready leader

- Status: Accepted
- Date: 2026-11-24
- Task: BL-5902 (fitness finding EXT-127)
- Decided by Claude under Stewart's delegation.

## Context

A Kafka broker that has just auto-created a topic can name a leader in Metadata and still
answer the Produce with NOT_LEADER_OR_FOLLOWER (6). The fitness review saw this in the first
seconds after a fresh `apache/kafka:3.9.0` start: exit 8, the record not stored. Curl already
asked Metadata again for errors 3 and 5 (ADR-1080, BL-5383), but ended at once on a Produce
error.

## Decision

- A Produce answered with UNKNOWN_TOPIC_OR_PARTITION (3), LEADER_NOT_AVAILABLE (5) or
  NOT_LEADER_OR_FOLLOWER (6) waits 250 ms, asks Metadata again and sends the same record
  batch again. The broker rejected it, so sending it again stores it once.
- Metadata and Produce share one budget of nine retries (2.25 s of waiting) per connection,
  so the two never multiply. Any other error, or one past the budget, ends with exit 8
  naming it. `-m` cancels a wait as before.
- The record is sent again over the same connection. A leader that moves to another broker
  during the retries is not followed; the transfer ends with exit 8.
- Fetch is unchanged: the finding is about produce, which loses the record.
