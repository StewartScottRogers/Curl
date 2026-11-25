# ADR-1369: A host name mismatch outranks a rejected certificate in the OpenSSL build

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation
- Task: BL-5926
- Corrects: ADR-1351, one statement of its measurements

## Context

ADR-1351 says a rejected `--cacert` certificate is reported as
`certificate rejected (28)` "before a host name mismatch". BL-5917 found curl doing the
opposite.

Measured again with curl 8.18.0 (OpenSSL 3.5.5) on Ubuntu against `openssl s_server`,
with a leaf for `good.test` and `--resolve`:

- A rejected root in `--cacert` or in `--capath`, or a rejected leaf alone in `--cacert`,
  asked for as `good.test`: exit 60,
  `SSL certificate OpenSSL verify result: certificate rejected (28)`.
- The same three asked for as `other.test`: exit 60,
  `SSL: no alternative certificate subject name matches target hostname 'other.test'`.
- The same three asked for as `127.0.0.1`: exit 60,
  `SSL: no alternative certificate subject name matches target ipv4 address '127.0.0.1'`.

## Decision

In the OpenSSL build a refused host name or address is reported ahead of a rejected
certificate, with `%{ssl_verify_result}` 1 as for every other refused name (ADR-0282).
The rest of ADR-1351 and ADR-1363 stands.

## Why

curl checks the name before it reads OpenSSL's verify result, and the drop-in rule is to
print what curl prints.
