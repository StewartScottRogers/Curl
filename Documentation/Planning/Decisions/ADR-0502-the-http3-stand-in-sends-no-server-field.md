# ADR-0502: The HTTP/3 stand-in sends no server field

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2268
- Decided by Claude under Stewart's delegation

## Context

The conformance harness's nghttpx-h3 stand-in (`Curl.Conformance.Http3Server.UnitLibrary`) read
sws's reply with the h2c stand-in's reader, which adds `server: nghttpx` as upstream's h2 cases
expect (2400, 2401, 2403, 2406). Upstream's HTTP/3 cases expect no `server` field: 2501 and 2503
list the reply's fields without one and strip nothing, and 2500 strips `server: nghttpx` with
`<stripfile>`, so it passes either way. Measured on Windows emulating the Linux build
(`UpstreamCurlPlatform.Unix`, `parsesAsWindowsBuild: false` and `--ssl-no-revoke` in place of the
Windows-only revocation check), 2503 got the header block, the body and the JSON, and differed only
at that one extra `server: nghttpx` line, in the header block and in `%{header_json}`.

## Decision

- `Http3ServerRequestRelay` drops the `server` field from the reply it relays, and its `502` for an
  unreachable backend carries none either. The h2c stand-in keeps `server: nghttpx`.

## Consequences

- 2503 passes off Windows and is listed in `PassingUpstreamCases.txt`; 2500 still passes.
- The Linux emulation of BL-2261 needs `--ssl-no-revoke` as well as the Unix platform and
  `parsesAsWindowsBuild: false`: without it every `--cacert` QUIC case fails on Windows with
  `schannel: the revocation status is unknown` (exit 60), which is what BL-2264 saw for 2503.
