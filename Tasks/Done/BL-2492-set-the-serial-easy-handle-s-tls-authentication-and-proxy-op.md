---
id: BL-2492
title: Set the serial easy handle's TLS, authentication and proxy options through CurlEasy.SetOption
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2451]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2492 — Set the serial easy handle's TLS, authentication and proxy options through CurlEasy.SetOption

## Goal

A serial transfer's EasyTlsOptions, EasyAuthenticationOptions and EasyProxyOptions reach its Libcurl CurlEasy as curl_easy_setopt calls (CurlEasy.SetOption, one per CURLOPT_* the command line sets), as upstream's config2setopts.c sets them, instead of being written whole onto the engine transfer's Options.

## Context

ADR-0506, ADR-0624; BL-2451 routed serial transfers through CurlEasy (ToolCurlEngine, ToolEasyHandle) and sets only CURLOPT_URL through SetOption; CurlCommandRunner.SetUpToolEasy still assigns ToolEasyHandle.Hooks.Options = EasyOptionsFromCommandLine.EasyOptionsOf(...). Turn TlsOf, AuthenticationOf and ProxyOf into SetOption calls for every field libcurl has a CURLOPT_* for, using Libcurl's setters (Libcurl/Options/<Group>) as they are. A field with no CURLOPT_* (a Curl extension such as the --httpsig-* options, or a value a setter would refuse or clamp differently from what the tool needs today) stays on the hooks' Options; record each such field and why in an ADR. A setter Libcurl lacks or that answers differently from the record is filed as a Libcurl task (touches Libcurl/Options/<Group>), not changed here. A non-Ok answer from SetOption must end the transfer as curl's tool does (its setopt failure path), measured on real curl where reachable.

## Acceptance criteria

- [x] Every EasyTlsOptions, EasyAuthenticationOptions and EasyProxyOptions field the command line sets with a CURLOPT_* equivalent is set through CurlEasy.SetOption; CurlCommandRunnerEasyTransferTests pins it for a representative option of each group.
- [x] The output is byte for byte unchanged: dotnet build is clean and the fast tests pass with no test deleted, skipped or weakened, and Curl.Conformance.UnitTests/PassingUpstreamCases.txt is unchanged or grown.
- [x] Measure-CodeQuality.ps1 -Library Curl.Console reports 0 failing members.
- [x] Curl.Console/CLAUDE.md says which fields go through SetOption and which stay on the hooks.

## Notes

- Plan: extend BL-2491's `EasySetoptsFromCommandLine` with a partial file of TLS (origin, `PROXY_*` twins, handle),
  authentication and proxy rows, and widen `KeptOnHooks` (ADR-0639). Followed ADR-0631's pattern directly; no
  architect stage was needed for a row table.
- Kept on the hooks, with why (ADR-0639): `--engine` (`CURLOPT_SSLENGINE` answers NOT_BUILT_IN; the engine's own
  setup failure prints curl's text), engine default, False Start, CA cache timeout, an `--ech` value
  `CURLOPT_ECH` would ignore, the TLS-SRP fields, and `--httpsig-*` (no `CURLOPT_*`).
- Order choices: `AWS_SIGV4` before `HTTPAUTH`; `HAPROXY_CLIENT_IP` before `HAPROXYPROTOCOL` (the setter turns the
  protocol on; the engine already sends the PROXY header for a client IP alone, so output is unchanged).
- No Libcurl setter needed a change, so no Libcurl task was filed. No command line reaches a refused setopt; the
  failure path is ADR-0631's, already pinned.
- Measured: fast tests all green (Curl.Console.UnitTests 2811 passed); Measure-CodeQuality -Library Curl.Console:
  0 failing members. PassingUpstreamCases.txt unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 9ce46de1 84393800 e3fd321b 908e5cf5.
- 2026-10-16: Doing -> Done. A serial transfer's TLS, authentication and proxy options reach its CurlEasy through SetOption (ADR-0639)
