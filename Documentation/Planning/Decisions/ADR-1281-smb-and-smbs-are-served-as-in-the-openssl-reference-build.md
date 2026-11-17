# ADR-1281: smb and smbs are served as in the OpenSSL reference build

Decided by Claude under Stewart's delegation (BL-5286). Supersedes ADR-0835.

## Context

ADR-0835 refused `smb://` and `smbs://` as disabled because the 8.22.0 reference builds measured then had no SMB. Surl's Phase 7 measurement shows upstream curl 8.21.0 built with SMB (OpenSSL) logging in over NTLM, downloading and uploading, with exit 67 for a wrong login, 78 for a missing share or file and 60 for an untrusted `smbs` certificate. Upstream is the oracle, and the port's rule is that nothing is left out. `Curl.Original.Protocol.Smb.UnitLibrary` already implements the client.

## Decision

`EngineComposition` registers `SmbProtocolHandler` for `smb` and `smbs`; `DisabledProtocolHandler` is removed. `curl -V` lists `smb` and `smbs` on every platform. `--proto`, `--proto-redir` and `--proto-default` know both schemes, and `--libcurl` writes them.

## Consequences

The Cgr043 tests that pinned the disabled answer are removed; the version-line tests pin the enabled one.
