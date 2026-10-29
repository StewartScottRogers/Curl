# ADR-1016: An extension certificate check can only refuse a chain curl accepted

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4097)

## Context

DANE/TLSA, Certificate Transparency and ML-DSA certificate extensions (ADR-0999) each need to check a
server's certificate chain. A drop-in replacement must never accept a chain curl refuses, so an
extension may add a refusal but never overrule curl's verification.

## Decision

- `Curl.Extension.Abstractions.Certificates.ICertificateCheckHook` names its activating option and is
  given, after curl's verification accepted the chain, the DER certificates the server sent (its own
  first) and the host name; it returns `null` to let the chain stand or the message the connect fails
  with as exit 60 (`CURLE_PEER_FAILED_VERIFICATION`), the code curl gives every other refused chain.
- The check sits outside the TLS providers, on the `ConnectResult` they already return: a failed result
  (curl's own refusal) is returned untouched without asking any hook, so no hook can accept it. This needs
  no change in `Curl.Tls` or `Curl.Networking`, and works for the `SslStream` and the hand-built providers
  alike.
- `CertificateCheckHooks.ActivatedBy` keeps every activated hook, unlike connection and resolver hooks:
  checks compose, each only narrowing what is accepted. They run in activator order and the first refusal
  disposes the connection and fails the connect.
- A reused connection was checked when it opened and is not checked again. A connector's connection that
  reported no certificate had no TLS and is not checked; a TLS provider's upgrade is always checked, even
  when its provider reported no certificate, so a provider that does not report its chain cannot let one
  through unchecked. A QUIC connect through `ConnectMultiplexedAsync` reports no chain and passes through;
  `ConnectMultiplexedSessionAsync` is checked.
- The check runs whenever curl accepted the chain, `-k` included: the extension's option asked for it, and
  the hook can read the options itself if it wants to stand aside under `-k`.
- `EngineComposition.CreateProtocolHandlers` takes `extensionCertificateConnector`, applied over the
  connector after `extensionConnector` so the check sees the extension transport's connections, and
  `extensionTlsProvider` for in-place upgrades (STARTTLS, `AUTH TLS`); `null` keeps curl's.

## Consequences

A host still has to build `CertificateCheckHooks` from its option group and pass both functions to the
engine; that wiring is its own task, as BL-4192 is for connection hooks. FTPS data connections opened
through `ftpDataConnector` are not checked yet.
