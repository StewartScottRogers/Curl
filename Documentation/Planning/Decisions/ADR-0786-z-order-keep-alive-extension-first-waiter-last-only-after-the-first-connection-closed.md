# ADR-0786 — The -Z start-order rule's keep-alive extension: the pending transfer goes last only when the host's first connection was closed

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3255
- Amends: ADR-0768
- Decided by Claude under Stewart's delegation.

## Context

ADR-0768 left open what curl does when the host's server keeps connections alive. BL-3189
measured curl 8.22.0 against a keep-alive loopback server (four URLs to one host):

| `--parallel-max` | curl 8.22.0 order with keep-alive |
| --- | --- |
| 50 (default) | URL order, every run: the pending transfer reuses the first connection |
| 2 | varies from run to run |
| 1 | URL order |

With a server that closes each connection the order is ADR-0768's (pending transfer last).

## Decision

1. ADR-0768's rule is extended: the first waiter on a host goes behind the transfers that start in
   the same step only when the host's first connection was closed when its transfer ended. When
   that connection stays open (keep-alive), the waiter reuses it and the transfers start in URL order.
2. At `--parallel-max` 1 the order is URL order either way.
3. At `--parallel-max` 2 with keep-alive curl's order varies between runs, so Curl pins no order there.

## Consequences

- Tests that pin the order use a closing server for the reordered case and a keep-alive server
  for the URL-order case at 50 and 1; none pins keep-alive at 2.
