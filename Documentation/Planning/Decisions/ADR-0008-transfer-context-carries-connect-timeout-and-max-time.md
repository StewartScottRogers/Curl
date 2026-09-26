# ADR-0008 — The transfer context carries the connect timeout and the maximum time

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

ADR-0003 put transfer options on `ITransferContext`, and ADR-0006 added the Phase 4
protocol options, listing its additions as "exactly these". Neither carries a timeout.

curl 8.21.0's `lib/tftp.c` (`tftp_set_timeouts`) computes the TFTP RRQ `timeout`
option, the retry interval and the retry count from the time left under the connect
timeout and the overall timeout (BL-075). A TFTP handler cannot reproduce that without
both values. The curl manpage (checked 2026-09-26, documenting curl 8.23.0) defines them:

- `--connect-timeout` limits only the connection phase
  (<https://curl.se/docs/manpage.html#--connect-timeout>).
- `-m`/`--max-time` limits the whole transfer
  (<https://curl.se/docs/manpage.html#-m>).

ADR-0003 and ADR-0006 are Accepted and so immutable
(`Documentation/Planning/Decisions/README.md`); this is a new decision alongside them.

## Decision

`ITransferContext` gains exactly these members:

- `TimeSpan? ConnectTimeout` - `--connect-timeout` as given; `null` when not given.
- `TimeSpan? MaxTime` - `-m`/`--max-time` as given; `null` when not given.

`TransferContext` implements both as `init` properties defaulting to `null`. The
values arrive as given; a handler that does not use them ignores them. Filling them
from the command line is BL-124; the first handler to read them is TFTP (BL-075).

## Consequences

Good:

- A handler can derive curl's per-protocol timeouts from the same two values curl
  uses, typed as `TimeSpan` rather than seconds in a `double`.
- `TimeProvider`, already on the context, lets those timeouts be tested without a
  real delay.

Costs and caveats:

- `ITransferContext` grows by two more members that most schemes do not read yet.
- `Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs` still implements the
  interface directly, so it gains both members too (ADR-0006 expects it to move to
  `TransferContext`).

## Alternatives considered

- **One `TimeoutPolicy` value holding both.** Rejected: curl treats them as two
  independent options, each optional, and a handler that reads one should not have to
  unpack the other.
- **Seconds as `double?`, as curl's command line takes them.** Rejected: the command
  line layer parses once and hands over a typed value, as it does for every other
  option; `TimeSpan` is what `TimeProvider` works in.
