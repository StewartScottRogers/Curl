# ADR-1390: The OpenSSL build checks name constraints name by name, from the top CA down (BL-5950)

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-1389.

## Context
`CertificateNameConstraints` checked every name against the permitted subtrees before any against
the excluded ones, and read the constraining CAs from the leaf upwards. Measured with curl 8.18.0
and OpenSSL 3.5.5 (WSL Ubuntu, `openssl s_server`, root, constrained intermediate and leaf), the
same answer with the root and with the intermediate as `--cacert` unless two are shown. Each exit
was 60.

| Leaf names | Constraints | `%{ssl_verify_result}` |
|---|---|---|
| Subject `O=Evil` with emailAddress `a@bad.org` | excluded directoryName `O=Evil`, permitted email `good.org` | 48 |
| SAN `email:a@bad.org`, `DNS:localhost` | excluded email `bad.org`, permitted DNS `other.org` | 48 |
| SAN `DNS:localhost`, `email:a@bad.org` | the same | 47 |
| SAN `IP:10.1.1.1`, `DNS:localhost` | excluded IP `10.0.0.0/8`, permitted DNS `other.org` | 48 |
| Subject emailAddress `a@bad.org`, SAN `DNS:localhost` | excluded email `bad.org`, permitted DNS `other.org` | 48 |
| Subject emailAddresses `a@good.org`, `b@bad.org`; SAN email `c@x.org` | excluded email `bad.org`, permitted email `good.org` | 47 |
| SAN `DNS:localhost` | root permits DNS `other.org`, intermediate excludes `localhost` | root: 47; intermediate: 48 |
| SAN `DNS:localhost` | root excludes DNS `localhost`, intermediate permits `other.org` | root: 48; intermediate: 47 |

## Decision
- The OpenSSL build reads the constraining CAs from the top of the chain (or the listed anchor)
  down. For each, it takes the leaf's names one at a time, permitted then excluded subtrees for
  each: the subject's directoryName, its emailAddresses, then the alternative names in the order
  the extension holds them. The first refusal is the answer, as OpenSSL's
  `check_name_constraints` and `NAME_CONSTRAINTS_check` give it.
- The Schannel build's answers do not change.
- This covers the verify result `CertificateNameConstraints` computes. Intermediates' own names,
  and a chain whose several constraint flags come from the platform's chain engine, are not
  reordered by this decision.
