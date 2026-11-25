# ADR-1354: The OpenSSL build checks only a host-name-like CN against DNS name constraints (BL-5829)

Status: Accepted. Decided by Claude under Stewart's delegation. Amended by ADR-1376.

## Context
`CertificateNameConstraints` evaluates the constraints the platform's chain engine skips under a non-self-signed anchor. It checked every subject CN of a leaf with no dNSName as a DNS name in both builds, which is CryptoAPI's rule (BL-5432). OpenSSL curl 8.18.0 accepts `CN=localhost` under `excluded;DNS:localhost` or `permitted;DNS:example.com` (STD-179): OpenSSL's `cn2dnsid` (`crypto/x509/v3_ncons.c`) reads a CN as a DNS name only when it looks like one.

## Decision
`FindViolation` takes `matchesSchannelBuild`. The Schannel build keeps CryptoAPI's rule. The OpenSSL build, the default, drops trailing NULs and checks a CN only when it is at least two dot-separated labels of ASCII letters, digits, `_` and `-`, none empty or beginning or ending with `-`. A CN with a NUL inside it refuses the chain, as OpenSSL does; it is reported as a permitted subtree violation (47) until BL-5921 reports OpenSSL's 53, so that narrowing the CN rule never accepts a name OpenSSL refuses for its NUL.

The parameter defaults to the OpenSSL rule because the callers that pass nothing serve only that build: the `--capath` rebuild and `OpenSslVerifyResult`.

## Consequences
The verdict for a dotless CN no longer depends on whether the root or an intermediate is the anchor. A CN in a multi-valued relative name is still not read by this check (BL-5921).
