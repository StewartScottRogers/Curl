# ADR-1326: The first QUIC attempt of a process leads TCP by a cold-start allowance

- Status: Accepted
- Date: 2026-11-20
- Task: BL-5660
- Decided by Claude under Stewart's delegation.

## Context

The first `--http3` (or `Http3`) request in a fresh process lost the QUIC-versus-TCP race: the hand-built QUIC and TLS 1.3 code is compiled when it first runs (BL-5635), which can take longer than curl's 200 ms happy-eyeballs delay. A QUIC connect that ignored its cancellation token could also run past the connect timeout.

## Decision

- `HttpProtocolHandler` gives the first QUIC attempt of the process (a shared `QuicColdStartGate`) a head start of the happy-eyeballs timeout plus `QuicColdStartAllowance`; `EngineComposition` sets it to 1 s. Later attempts, and `--happy-eyeballs-timeout-ms` itself, are unchanged. Cost: the first `--http3` attempt against a host that drops UDP starts TCP up to 1 s later, once per process.
- `HttpTransferDeadline` ends a connect at the connect timeout with `WaitAsync`, so a connector that ignores its token cannot hang the transfer.

## Consequences

The race stays curl's after the first attempt. The allowance is a cap on a one-time cost, not a measurement of it; a loopback HTTP/3 server was not available to measure the stages in this lane.
