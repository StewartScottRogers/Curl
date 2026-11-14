# ADR-1269: The OpenSSL build names a chain's CA, path-length, name-constraint and signature failures by their verify result

- Status: Accepted
- Date: 2026-11-13
- Task: BL-5104
- Decided by Claude under Stewart's delegation.

## Context

curl 8.18.0's OpenSSL build prints the X509_V code OpenSSL's `X509_verify_cert` stops at:
`certificate signature failure (7)`, `path length constraint exceeded (25)`,
`permitted subtree violation (47)` and `invalid CA certificate (79)` for a chain it built to a
trusted root that fails a later check (the outside fitness review's STD-034). The port printed
`self-signed certificate in certificate chain (19)` for every one of them, since any status
other than a lone `NotTimeValid` was reported as a trust error. .NET folds both
`X509_V_ERR_INVALID_CA` and `X509_V_ERR_PATH_LENGTH_EXCEEDED` into
`X509ChainStatusFlags.InvalidBasicConstraints`.

## Decision

- `OpenSslVerifyResult.OfExtensionsOrSignature` maps the flags of a chain that carries neither
  `UntrustedRoot` nor `PartialChain` in OpenSSL's order: `check_chain_extensions` first
  (`InvalidBasicConstraints` is 25 when a CA's path length is exceeded, else 79), then
  `check_name_constraints` (`HasNotPermittedNameConstraint`, 47), then `internal_verify`
  (`NotSignatureValid`, 7), and only then the dates (9, 10). Trust errors (18, 19, 20) still
  win, and the purpose refusal (26) keeps its place before these.
- 25 is told from 79 by counting, as `check_chain_extensions` does: a CA at depth `d` whose
  basic constraints carry a path length below `d - 1` is exceeded
  (`OpenSslVerifyResult.ExceedsAPathLength`). Self-issued intermediates are counted too, a
  simplification for a case no measured chain holds.
- `TlsFailureMessages.OpenSslExtensionOrSignatureError` gives the text, from OpenSSL's
  `X509_verify_cert_error_string`; `OfChainStatus` returns the same codes for `-w
  %{ssl_verify_result}` and the handshake event.

## Consequences

The exit code (60) is unchanged; only the text and the reported verify result change. An
excluded-subtree violation (48) and other OpenSSL codes are still not mapped.
