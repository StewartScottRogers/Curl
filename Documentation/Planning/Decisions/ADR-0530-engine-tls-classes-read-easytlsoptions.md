# ADR-0530 — The engine's TLS classes read `EasyTlsOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2324.

## Context

ADR-0506 plan phase 1 step 4 moves the engine off `CommandLineOptions` one option group at a time.
ADR-0514 settled how the command line maps onto `EasyTlsOptions`. BL-2324 has the engine's TLS
classes read that record, with behaviour byte for byte unchanged.

## Decision

- `TlsClientOptionsMapping.Of`, `ProxyOf` and `DohOf` map `EasyTlsOptions` onto the provider's
  `TlsClientOptions`. `--no-alpn` (`UseAlpn`) and `--ssl-sessions` (`SslSessionsFile`) are not on the
  record (ADR-0514), so they are passed beside it. `SslVersion.Tls1` and `Tls10` are `TlsVersion.Tls10`;
  `SslVersion.Default` and `SslVersionMaximum.None`/`Default` are `TlsVersion.SystemDefault`.
- `EngineComposition.CreateTransports`, `CreateDnsResolver`, `CreateDohConnector`, `CreatePoolingConnector`
  and `OptionGroupConnectionSettings.Of` take the record beside `CommandLineOptions`; `Curl.Console`
  builds it with `EasyOptionsFromCommandLine.TlsOf`. `TlsBuildInformation.Lines` reads `--engine list`
  from `EasyTlsOptions.Engine` (exactly `list`, which ends the command line) and takes
  `--dump-ca-embed`, the tool's own, as a flag. `TransferContextFactory` had no TLS fields to move.
- `-E` is split once, in `TlsOf`. `ClientCertificateLoader` loads `TlsClientOptions.ClientCertificate`
  as the path and `Passphrase` as the pass phrase, without splitting again, so an escaped `\:` is
  unescaped once, as curl's tool does.
- `TlsPeerOptions.CertificateType` and `KeyType` become nullable, default `null`, and `TlsOf` sets them
  only when `--cert-type`/`--key-type` (or their proxy twins) are given. libcurl stores no type by
  default; the OpenSSL build reads none as PEM, but the Schannel build reads none as P12 and refuses an
  explicit `PEM` with "certificate format compatibility error". A `"PEM"` default would have made
  every Schannel `-E file.p12` fail.
- `TlsOf` no longer guesses `ENG` for a `pkcs11:` certificate or key without a type (amending
  ADR-0514): the engine would then refuse it with the engine-not-set text where today it reports the
  PEM load failure, a behaviour change this retarget must not make. Matching curl's tool there needs
  real curl measured first; BL-2349 does that.

## Consequences

The engine's TLS settings come only from `EasyTlsOptions` (plus `UseAlpn` and `SslSessionsFile`, until
their own groups move them). Tests that drove a TLS mapping now go through `TlsOf`, or build the
record for `TlsBuildInformation`; Networking tests split `-E` values before handing them to the loader.
