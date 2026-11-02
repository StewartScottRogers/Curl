# ADR-1191: gs:// signing is verified from the V4 documentation, and a region comes from --aws-sigv4

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4476
- Decided by Claude under Stewart's delegation.

## Context

BL-4476 measures the `gs://` client's HMAC-key signing (`--user ACCESS_ID:SECRET`) against Cloud Storage's
"V4 signing process" in the `Authorization` header form. It needs a verifier independent of the client's `Signing/`
code, a way to name a region other than `auto`, and a matrix in a second file for the same protocol.

## Decision

- The reference endpoint's `GcsV4HmacReferenceAuthorizer` recomputes the canonical request, string to sign and
  HMAC-SHA256 key chain from the documentation with BCL `HMACSHA256` and `SHA256`, re-encoding the decoded path and
  query itself rather than trusting the client's encoding, and refuses anything else with
  `403 SignatureDoesNotMatch`, the code Cloud Storage answers. Its fixed vector was computed by hand in PowerShell
  from the documented steps; the documentation's own worked example is a signed URL with an RSA key, which a
  `gs://` HMAC request cannot use.
- A region other than `auto` is named with `--aws-sigv4 goog4:goog:<region>:storage`, curl's own
  `provider1:provider2:region:service` form, whose provider names already give `GOOG4-HMAC-SHA256` and the
  `x-goog-` header prefix. The client ignores it today; BL-4542 implements it.
- A protocol split into parts keeps every part's IDs as `<Name>:`; the matrix check counts an ID in `<Name>.md` or
  any `<Name>-<Part>.md`, as `Documentation/Conformance/Extended/README.md` already says the files are named.

## Consequences

Signing cases run at a fixed clock through the run's `TimeProvider`. The verifier also checks the 15-minute clock
window and that `host` and every `x-goog-*` header are signed, so a client that leaves one unsigned fails.
