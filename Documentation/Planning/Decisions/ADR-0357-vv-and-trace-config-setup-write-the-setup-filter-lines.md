# ADR-0357 — `-vv` and `--trace-config setup` write the setup filter's lines; `-vv` to `-vvvv` turn on trace components

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1103, following ADR-0318 and ADR-0356: curl 8.21.0 (Schannel) writes per-component trace lines for
its connection filters and transfer engine. Measured on 2026-10-02 with `Record-CurlExchange.ps1`
(BL-1103 Notes):

- `-vv` and `--trace-config setup -v` write four `[SETUP]` lines around a direct connect: `added`
  before anything else, `happy eyeballing to origin <host>:<port>` after the resolve lines and before
  the first `Trying`, and `removing connected setup filter` and `destroy` after `Established
  connection` (after the `[DNS]` filter's own removal when both are on). A connect that fails writes
  only the first two.
- `-vvv` adds `[READ]` and `[WRITE]`; `-vvvv` writes every component, as `--trace-config all`.
- Order matters: `--trace-config -setup -vv` keeps `[SETUP]`, `-vv --trace-config -setup` drops it.
  `-vv -v` drops it (a first `-v` resets), while `--trace-config dns -v -v` keeps `[DNS]`.
  `--no-verbose` drops every component, even ones `--trace-config` named; so does `-all`.
- `network` writes `[DNS]`, `[HAPPY-EYEBALLS]`, `[TCP]`, `[MULTI]` and `[TIMER]`, but no `[SETUP]`.
- `[HAPPY-EYEBALLS]`, `[TCP]`, `[MULTI]` and `[TIMER]` lines carry socket descriptors, nanosecond
  progress stamps and poll-loop repetitions whose counts differ from run to run.

## Decision

1. `Curl.Cli` adds the components each verbosity brings to `CommandLineOptions.TraceComponents`
   (`setup` at 2, `read` and `write` at 3, `all` at 4) and remembers which ones it added, so a first
   `-v` takes those out again unless a later `--trace-config` named them; `--no-verbose` and
   `--trace-config -all` empty the set. The console layer then needs only the set.
2. `TcpConnector.TracesSetupFilter` (`CurlComposition.TracesSetup`: `setup` or `all`) writes
   `[SETUP] added` and wraps a direct connect's events in `SetupFilterTraceEvents`, layered over
   `DnsFilterTraceEvents` so the two filters' lines interleave in curl's order.
3. `network` also turns on the `[DNS]` lines (`CurlComposition.TracesDns`).
4. This run delivers `[SETUP]` only. The other components' lines are split into follow-up tasks, each
   to record how its volatile values (descriptors, nanoseconds, poll repetitions) are produced:
   `[HAPPY-EYEBALLS]` and `[TCP]`; `[MULTI]`, `[TIMER]`, `[READ]` and `[WRITE]`; and the proxy filters'
   and `[HTTPS-CONNECT]`'s lines.

## Alternatives considered

- Deriving `-vv`'s components from `Verbosity` in the console layer: loses the measured ordering
  (`--trace-config -setup -vv` against `-vv --trace-config -setup`), which only the parser sees.
- Folding the `[SETUP]` lines into `DnsFilterTraceEvents`: one class for two components, and its name
  would no longer say what it does.

## Consequences

`curl -vv` now prints curl's four `[SETUP]` lines for every direct TCP connect, and `--trace-config`
names that the console does not yet write lines for are already in the set, ready for their tasks.

## Amendment, 2026-10-02 (BL-1159): the `[READ]` lines

Decided by Claude under Stewart's delegation.

BL-1159 split again (its Context allows it): this run delivers the `[READ]` lines of a plain
transfer; `[TIMER]`, `[WRITE]` and `[MULTI]` each go to a follow-up task of their own, because
`[TIMER]` is written inside `Curl.Networking`'s connector and `[WRITE]` and `[MULTI]` from the HTTP
handler's client writer stack and a transfer engine Curl does not have as such.

- Measured (BL-1159 Notes): `[READ] client_reset, clear readers` is written once as the transfer
  starts - after the `--resolve` entries' `Added ... to DNS cache` lines, before anything resolved or
  dialled, `[<xfer>-x]` under `--trace-ids` - and once more as a finished transfer resets, before its
  connection's `left intact` or `shutting down connection #N` line; a failed transfer (a refused
  connect, `-f` on a 404) writes no second line.
- `Curl.Console` writes the first line itself (`CurlCommandRunner.TraceClientReaderReset`) and wraps
  the transfer's events in `ClientReaderResetTraceEvents` for the second, under
  `CurlComposition.TracesRead` (`read` or `all`, which `-vvv` and `-vvvv` put there; `network` does not).
- The `[READ]` lines carry no volatile value. The upload readers' lines (`add buf reader`,
  `cr_buf_read(len=65388)`, `client_read(...)`) of a `-d` body, and the lines of each followed
  redirect's hop, were not delivered here and are a follow-up task.
