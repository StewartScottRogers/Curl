# ADR-1369: `-d` with `-T` stays refused for extended protocol schemes

- Status: Accepted
- Date: 2026-11-24
- Task: BL-5901 (fitness finding EXT-126)
- Decided by Claude under Stewart's delegation.

## Context

The conformance documents of the extended protocols that upload with `-T` (`kafka`, `grpc`,
`stomp`, `signalr`, `webtransport` and others) said that `-d` wins when both `-d` and `-T` are
given. From the command line that never happens: Curl refuses the two together at transfer setup,
for every scheme, with `Warning: You can only select one HTTP request method! You asked for both
PUT (-T, --upload-file) and POST (-d, --data).` and exit 2.

Real curl does the same whatever the scheme: curl 8.22.0 (Schannel, Windows, 2026-11-24) printed
that warning and ended with exit 2 for `curl -S -d dwins -T up.txt kafka://127.0.0.1:1/t/0` and
for the same command against `mqtt://127.0.0.1:1/t`, and with `-s` ended with exit 2 silently.

## Decision

The command line keeps refusing `-d` with `-T` for every scheme, extended protocols included. The
documents are corrected to say so.

## Why

An extension never changes curl's behaviour (ADR-0999), and the refusal belongs to the command
line, which curl applies before it looks at the protocol. Exempting some schemes would make one
option rule depend on the URL, for no gain: `-d @file` or `-T file` alone already sends any bytes.

## Consequences

A protocol handler that reads its payload from the `-d` data, else from the upload, keeps that
order for a request that carries both; the command line never builds one.
