# ADR-1377: A Kafka produce follows a leader that moves to another broker during its retries

- Status: Accepted
- Date: 2026-11-25
- Task: BL-5934
- Decided by Claude under Stewart's delegation.
- Supersedes ADR-1370's "a leader that moves to another broker during the retries is not followed" item and its
  per-connection retry budget, and ADR-1080's "it does not follow a second redirect" for Produce.

## Context

ADR-1370 asks Metadata again after a Produce answered with error 3, 5 or 6, but sent the record again over the same
connection. In a cluster where leadership moved to another broker, every retry failed and the transfer ended with exit 8.

## Decision

- When the Metadata asked after a refused Produce names a leader other than the connected broker (host compared without
  case, and port, as in ADR-1080), nothing more is sent on that connection. The handler closes it, connects to the new
  leader with the URL's TLS choice and the transfer's proxy and logs, authenticates when the transfer has `--user`, and
  sends the same record batch, with its original timestamp. It does not ask the new leader for Metadata first.
- The nine retries belong to the transfer, not to a connection, so a leader that keeps moving costs at most nine waits
  of 250 ms and ten Produce requests. The one after the last retry ends the transfer with exit 8 naming its error.
- A new leader that cannot be connected to ends the transfer with the connector's exit code, as the first redirect does.
- Fetch is unchanged.

## Consequences

The first redirect's connection now shares the budget with the URL broker's connection: Metadata retries used before
that redirect leave fewer for the Produce.
