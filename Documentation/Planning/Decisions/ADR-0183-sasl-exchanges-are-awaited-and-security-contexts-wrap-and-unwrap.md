# ADR-0183 — SASL exchanges are awaited, and security contexts wrap and unwrap

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-851.

## Context

BL-538 answers SASL `GSSAPI` and `NTLM` through the `ISecurityContext` seam (ADR-0142,
ADR-0176). Two things stood in its way:

- `ISaslExchange.InitialResponse` and `Respond` were synchronous (ADR-0121), but
  `ISecurityContext.NextTokenAsync` is asynchronous because the hand-built Kerberos route asks
  a KDC for the service ticket on its first step. SASL GSSAPI's initial response is that first
  token, and `.Result` and `.Wait()` are forbidden.
- RFC 4752 section 3.1: once the GSSAPI context is established, the server sends a wrapped
  four-byte security-layer offer and the client answers with a wrapped four-byte choice.
  `ISecurityContext` had no `GSS_Wrap` or `GSS_Unwrap`. `Curl.Kerberos`'s `KerberosGssContext`
  has them (ADR-0171), and so does the BCL's `NegotiateAuthentication`.

## Decision

1. **The SASL exchange is awaited.** `ISaslExchange` has
   `ValueTask<byte[]?> GetInitialResponseAsync(CancellationToken)` and
   `ValueTask<byte[]?> RespondAsync(ReadOnlyMemory<byte>, CancellationToken)`, with the old
   meanings: `null` from the first is "the mechanism has no initial response", `null` from the
   second is "cannot answer", so the handler cancels with exit 67. `ISaslAuthenticator.Begin`
   stays synchronous; it does no I/O.
2. **Handlers ask for the initial response once, before the command.** SMTP, IMAP and POP3
   await `GetInitialResponseAsync` with the transfer's `CancellationToken` before they send
   `AUTH`/`AUTHENTICATE`, then decide as before whether it goes inline or answers the first
   challenge. For every mechanism built today the response is fixed at `Begin`, so no byte on
   the wire changes; for GSSAPI the ticket is fetched before the command, which the mail
   server cannot see.
3. **Wrap and Unwrap are members of `ISecurityContext`, not an optional interface.**
   `byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt)` and
   `byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage)`. An optional interface would be lost
   behind `SspiNegotiateSecurityContext` and `FallbackSecurityContext`, which wrap another
   context, and every caller would have to test for it; as members, those two simply delegate.
   The seam's fakes grew the two members.
4. **Refusals.**
   - Not established: `InvalidOperationException`, on every route.
   - curl's own NTLM (`HandBuiltNtlmSecurityContext`): `NotSupportedException` always, since
     it keeps no session key (curl's SASL NTLM has no security layer either).
   - A wrapped message that is malformed, altered or out of sequence: `Unwrap` answers `null`
     (`KerberosGssException` on the hand-built route, a status other than `Completed` from the
     BCL). `Wrap` answers `null` when the BCL refuses to wrap.
5. **Protection is asked for per request.** `SecurityContextRequest.MessageProtection`
   (`System.Net.Security.ProtectionLevel`, default `None`) becomes the BCL's
   `RequiredProtectionLevel`. SSPI's NTLM negotiates no signing key without it, so its
   wrapped messages do not verify (measured in `SystemSecurityContextFactoryTests`). HTTP
   leaves it at `None`, so the NTLM and Negotiate tokens stay as curl sends them. Which level
   SASL GSSAPI asks for is BL-538's to measure against curl.

## Consequences

- `Curl.Authentication.UnitTests` pins wrap and unwrap on the system route (Windows: NTLM
  against an in-process SSPI acceptor) and the hand-built Kerberos route (against
  `FakeGssAcceptor`, both directions, sealed and signed-only, and an altered token).
- BL-538 can build SASL GSSAPI and NTLM exchanges over `ISecurityContextFactory` with no
  further contract change.

## Alternatives considered

- **An optional `ISecurityMessageProtection` interface.** Lost: the two decorating contexts
  would each need to implement it conditionally, and a caller could not tell "no protection"
  from "hidden behind a decorator".
- **A synchronous `InitialResponse` with the first token computed lazily in `RespondAsync`.**
  Lost: under `--sasl-ir` the initial response goes on the command line, before any
  challenge, so it has to be available before the command is sent.
- **Returning a status record from Wrap and Unwrap.** Lost: the only outcomes a caller acts on
  are "here are the bytes" and "it did not verify"; exceptions cover the programming errors.
