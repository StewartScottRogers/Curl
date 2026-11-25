# ADR-1388: The OpenSSL build refuses a subject emailAddress that is not an IA5String under name constraints (BL-5942)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1387.

## Context
`CertificateNameConstraints` decoded a subject emailAddress of any character string type and
checked it as an rfc822Name. Measured with curl 8.18.0 and OpenSSL 3.5.5 (WSL Ubuntu,
`openssl s_server`, subject CN `localhost` last, SAN `DNS:localhost`, a constrained intermediate
given as `--cacert`):

| Subject emailAddress and other names | Constraint | Answer | `%{ssl_verify_result}` |
|---|---|---|---|
| UTF8String `a@example.com` | permitted email `example.com` | exit 60, `unsupported or invalid name syntax (53)` | 53 |
| BMPString `a@example.com` | permitted email `example.com` | exit 60, 53 | 53 |
| PrintableString `example.com` | excluded DNS `other.org` | exit 60, 53 | 53 |
| UTF8String | excluded DNS `other.org` | exit 60, 53 | 53 |
| UTF8String | none | exit 0 | 0 |
| UTF8String, after `O=Evil` | excluded directoryName `O=Evil` | exit 60, `excluded subtree violation (48)` | 48 |
| UTF8String | excluded DNS `localhost` (the SAN) | exit 60, 53 | 53 |
| IA5String `a@other.org`, then UTF8String | excluded email `other.org` | exit 60, 48 | 48 |
| UTF8String, then IA5String `a@other.org` | excluded email `other.org` | exit 60, 53 | 53 |

An emailAddress encoded as an OCTET STRING never gets this far: OpenSSL cannot load the
certificate.

## Decision
- Under name constraints the OpenSSL build checks the subject's directoryName, then each subject
  emailAddress in order. The first emailAddress that is not an IA5String answers 53, whatever the
  constraints' kinds, and no alternative name or CN is checked after it, as OpenSSL's
  `NAME_CONSTRAINTS_check` does.
- The Schannel build's answers do not change.
