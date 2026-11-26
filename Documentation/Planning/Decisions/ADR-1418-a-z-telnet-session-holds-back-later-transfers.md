# ADR-1418: Under -Z a telnet session holds back later transfers, and telnet -t values are not judged at setup

- Status: Accepted
- Date: 2026-11-26
- Task: BL-6017

Decided by Claude under Stewart's delegation.

## Context

Fitness finding STD-183: under `-Z`, Curl ended the whole run with exit 43 when a telnet
`-t NEW_ENV=` had no comma, or a `TTYPE` or `XDISPLOC` value was too long (BL-5811, whose limits
were not measured).

Measured on curl 8.22.0 (Schannel, Windows) on 2026-11-26, against a loopback telnet server
sending `IAC DO NEW-ENVIRON` and `IAC DO TTYPE` and an HTTP server, standard input `NUL`:

- With no subnegotiation from the server, none of the three values fails, with or without `-Z`.
  The session ends as it would otherwise (exit 0 once the server closes, 28 under `-m`).
- When the server sends `SB ... SEND` subnegotiations, the transfer fails with exit 43.
- Under `-Z` the telnet transfer's output precedes the HTTP one's, with or without
  `--parallel-max 1`: curl runs the whole session inside one call of its parallel loop.

## Decision

1. Setting up a `-Z` telnet transfer fails only for an unknown `-t` option (exit 48, BL-5469).
   Option values are left to the protocol handler, which judges them when the server asks.
2. Under `-Z` the runner starts no later transfer until a telnet transfer has ended.

## Consequences

- A long telnet session delays every transfer after it, as in curl.
- Transfers already running when a telnet transfer starts carry on; curl pauses them too, which
  Curl does not reproduce.
- The `[43]`, `[42]` expectation BL-5811 took from an earlier review is withdrawn.
