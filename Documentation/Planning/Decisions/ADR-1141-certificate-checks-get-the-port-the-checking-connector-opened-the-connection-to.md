# ADR-1141 — Certificate checks get the port the checking connector opened the connection to

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4331.

## Context

DANE (BL-4151) looks up TLSA records at `_<port>._tcp.<host>` (RFC 6698 section 3), but
`ICertificateCheckHook.RefusalOfAsync` (ADR-1016) gave a check only the host. `CertificateCheckingConnector`
has the `ConnectTarget`, so its port is known. `CertificateCheckingTlsProvider`, which checks in-place upgrades
(SMTP, IMAP, POP3 and LDAP STARTTLS, FTP `AUTH TLS`), gets only the plaintext `IConnection` and the target host
through `ITlsProvider`, and `Curl.Protocol.Abstractions` should not change for an extension.

The plaintext connection's `RemoteEndPoint` was considered and rejected: through a proxy tunnel (HTTP `CONNECT`,
SOCKS, an HTTP/2 tunnel) it is the proxy's address, so its port would name the wrong TLSA record, and the
provider cannot tell a tunnelled connection from a direct one.

## Decision

- `RefusalOfAsync` takes `int? port` after the host: the TCP port the checked connection was made to.
- **Connector path:** the port is `ConnectTarget.Port`, for a plain connect and a multiplexed session alike.
- **Provider path:** `CertificateCheckHooks` keeps, in a `ConditionalWeakTable`, the target of every connection
  the checking connector returned. An upgrade of one of those connections is checked with that target's port,
  the port curl dialled (the server's, never a proxy's). A connection the checking connector did not return -
  one the engine wrapped after the connector, or a stand-in in a test - is checked with `null`, meaning
  "unknown"; a check that needs the port (DANE) decides what an unknown port means. The engine applies the
  checking connector outermost of the connectors (`EngineComposition`), so a protocol's control connection is
  the object the table holds. FTP's `AUTH TLS` on a data connection gets that data connection's own port.
- The table is weak, so an entry goes with its connection and nothing is disposed or removed by hand.

## Consequences

- No change to `Curl.Protocol.Abstractions`; the two existing checks (Certificate Transparency, ML-DSA
  certificates) take the new parameter and ignore it.
- A future wrapper the engine puts around connections after the checking connector would make the provider path
  give `null`; the unit test that upgrades a connection the checking connector opened shows the port arriving.
