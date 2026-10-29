# ADR-0979: A connect target names its TLS server name and can ask for system-store-only trust

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4026, for CGR-289).

## Context

On Windows curl's `ldaps://` goes through WinLDAP, whose second connection sends the machine
name as SNI and verifies the server against the Windows certificate store whatever `--cacert`,
`--capath` or `-k` say (measured, BL-3818). `ConnectTarget` could only name the host it dials,
which TLS also sent as SNI and checked the certificate against, and every handshake trusted as
the transfer's TLS options said.

## Decision

- `ConnectTarget.TlsServerName` names the host the handshake sends as SNI and checks the server
  certificate against; `null`, the default, keeps `Host`. The connector still resolves and dials
  `Host`.
- `ConnectTarget.TrustsSystemStoreOnly` asks for a handshake that trusts only the system store.
  `TcpConnector.TrustedAs` then runs it through `ISystemStoreTrustTlsProvider.WithSystemStoreOnlyTrust`,
  which both `SslStreamTlsProvider` and `HandBuiltTlsProvider` implement as a copy of themselves
  (same build, clock, certificate store, session cache) whose options drop `Insecure`,
  `CaCertificateFile`, `CaCertificateDirectory` and `CaCertificateBlob`. Every other setting,
  `--pinnedpubkey` and `--crlfile` included, is kept. A provider without the interface (a test
  fake) is used as it is.
- The derived provider is made per handshake rather than cached: only the WinLDAP path asks for
  it, once per transfer, and making one reads no file.

## Consequences

BL-3818 sets both on the second ldaps connection. No other handler sets them, so every existing
connect behaves as before.
