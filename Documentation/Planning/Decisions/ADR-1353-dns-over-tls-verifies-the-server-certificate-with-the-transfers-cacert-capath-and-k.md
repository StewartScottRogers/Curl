# ADR-1353: --dns-over-tls verifies the server certificate with the transfer's --cacert, --capath and -k

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5872).

## Context

`--dns-over-tls` built its TLS provider with default options, so a DoT server whose certificate chains to a private CA was always rejected (exit 6), even under `--cacert` or `-k`. `--doh-url` uses the transfer's trust settings.

## Decision

`ExtensionOptionValues` carries an `ExtensionTlsTrust` (`Insecure`, `CaCertificateFile`, `CaCertificateDirectory`) that `CurlComposition.ExtensionOptionValuesOf` fills from `-k`, `--cacert` and `--capath`. `EncryptedDnsExtension.ActivateDnsOverTls` turns it into the `TlsClientOptions` of `DnsOverTlsResolverHook`. There is no `--dns-over-tls-insecure` override, so the transfer's options are the only ones. `--dns-over-quic` is unchanged (it judges the certificate through the connector's verifier).

## Consequences

The option help and the extension's CLAUDE.md say which trust options are honoured. Other extensions may read `TlsTrust` the same way.
