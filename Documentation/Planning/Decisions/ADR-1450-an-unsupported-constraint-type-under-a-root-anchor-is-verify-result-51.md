# ADR-1450: An unsupported constraint type under a root anchor is verify result 51 (BL-6059)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1383 and ADR-1442.

## Context
With a self-signed root as `--cacert`, the platform chain engine judges the name constraints and .NET gives one flag, `HasNotSupportedNameConstraint`, for several OpenSSL errors, which the OpenSSL build reported as 53 (ADR-1383). curl 8.18.0 with OpenSSL 3.5.5 answers `unsupported name constraint type (51)` for an otherName under a constraint of its type-id, root anchor or not (measured, ADR-1442).

## Decision
For a chain flagged `HasNotSupportedNameConstraint` and no subtree refusal, the OpenSSL build runs its own name constraint check (`CertificateNameConstraints.FindViolation`). Where the first refusal it finds is the unsupported constraint type, 51 replaces the flag. Where it finds nothing, or a subtree violation the platform engine did not flag, the engine's flag stands and the answer is 53. A chain flagged with a subtree refusal is handled as before.

## Consequences
- A UPN, or another otherName, under a constraint of its type-id is exit 60 with 51 under a root anchor too.
- The Schannel build's answers do not change.
