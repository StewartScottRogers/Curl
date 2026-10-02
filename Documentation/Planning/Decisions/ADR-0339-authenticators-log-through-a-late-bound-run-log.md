# ADR-0339 — The authenticators log through a late-bound run log, by scheme name and status only

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-923.
It applies ADR-0222 (the diagnostic log) to `Curl.Authentication.UnitLibrary`.

## Context

BL-923 asks the HTTP and SASL authenticators and the security-context router to log their
choices (component `auth`), taking `IDiagnosticLog` through their constructors. The
composition root builds them inside each option group's `TransferDispatch`, through the
`Func<CommandLineOptions, TransferDispatch>` the runner is given. The runner opens the run's log
from `--log-level` and `--log-file` only after the command line is parsed, and that factory is
given only the options, not the log.

## Decision

1. `Curl.Console` gets `LateBoundDiagnosticLog`. Each `CurlComposition.CreateRunner` makes one,
   captures it in the dispatch factory it gives the runner, and passes it to the runner, which
   `Bind`s it to the run's log in `OpenDiagnosticLogAsync`. Until then it writes nothing. The
   composition methods (`CreateProtocolHandlers`, `CreateTransports`, `CreateHttpAuthenticator`,
   `CreateSaslAuthenticator`, `CreateSecurityContextFactory`, `CreateProxyTunnelOptions`) take an
   optional `IDiagnosticLog`, and `CurlTransports` carries it, so the origin's and the proxy's
   authenticators both log.
2. In `Curl.Authentication`, the constructors take an optional `IDiagnosticLog? diagnosticLog = null`
   as their last parameter, so every existing caller and test is unchanged. One internal
   `AuthDiagnosticLog` builds every message and tests `IsEnabled` first.
3. Messages are built only from scheme, mechanism, algorithm, qop and status names, exit codes,
   fixed reasons and the host: never a user name, password, token, message byte or header value.
   The scheme sets are written as `HttpAuthSchemes` names (`Basic, Digest`, `Any`).
4. Levels: `error` for NTLM and SSPI DIGEST-MD5 failures that end the transfer; `warning` for no
   allowed scheme, no usable SASL mechanism, a Negotiate context with no token, and an NTLM round
   that makes nothing or is refused; `info` for the scheme or mechanism chosen and what was
   offered; `verbose` for each NTLM round, each SASL exchange begun, the Digest algorithm and qop,
   and which security context the router chose and why.
5. The netrc match, the SigV4 scope, the Negotiate rounds and the hand-built SPNEGO choice are
   left to BL-1151, to keep BL-923 one run.

## Consequences

A line is logged only once the run's log is bound, which is before any transfer starts. A test
that builds an authenticator without a log sees no change.

## Alternatives considered

- Passing the log through `HttpAuthRequest` or `SaslRequest`: changes the shared contract in
  `Curl.Protocol.Abstractions`, which every protocol task overlaps, and the task names
  constructor injection.
- Building the dispatch only after the log opens, with a factory that takes the log: changes the
  runner's factory signature and every test that passes one.
