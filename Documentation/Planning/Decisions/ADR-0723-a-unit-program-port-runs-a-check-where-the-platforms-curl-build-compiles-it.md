# ADR-0723 — A unit-program port runs a check only where the platform's curl build compiles it

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2843
- Decided by Claude under Stewart's delegation.

## Context

Many of upstream curl 8.22.0's unit programs wrap their checks in TLS-backend guards, and
print a line and return 0 where the guard is false. Of the nine cases BL-2843 names:

| Case | Guard | Schannel build (Windows) | OpenSSL build (Linux, macOS) |
| --- | --- | --- | --- |
| 1394 `tool1394` | `#ifdef _WIN32` around the drive-letter rows | every row | rows without a drive letter |
| 1397 | `USE_OPENSSL \|\| USE_SCHANNEL` | checks | checks |
| 1651 | `USE_GNUTLS \|\| USE_SCHANNEL` | checks | `not tested since Curl_extract_certinfo() is not built in` |
| 1656 | `USE_GNUTLS \|\| USE_SCHANNEL \|\| USE_MBEDTLS` | checks | `not tested since Curl_x509_GTime2str() is not built in` |
| 1657 | same | checks | `not tested since getASN1Element() is not built in` |
| 1666 | same (and more) | checks | `not tested since encodeOID() is not built in` |
| 1667 | same (and more) | checks | `not tested since ASN1tostr() is not built in` |
| 1676 | `USE_GNUTLS \|\| USE_MBEDTLS \|\| USE_RUSTLS \|\| USE_SCHANNEL` | checks | `not tested since Curl_extract_certinfo() is not built in` |
| 3205 | `USE_MBEDTLS \|\| USE_RUSTLS` | nothing, returns 0 | nothing, returns 0 |

ADR-0009 matches the platform's curl: the Schannel build on Windows, the OpenSSL build
elsewhere. ADR-0711 says how a port is shaped, not which guarded branch it takes.

## Decision

A port takes the branch the platform's curl build compiles, chosen at run time with
`OperatingSystem.IsWindows()`, the same split ADR-0009 makes everywhere else:

- On Windows it runs the Schannel build's checks. On Linux and macOS a check the OpenSSL
  build leaves out is not run, and the port writes the C program's `not tested since ...`
  line to its standard output, as `puts` does, and passes.
- `tool1394`'s `_WIN32` rows run on Windows only, through
  `ClientCertificateArgument.Split(value, recognisesDriveLetters: OperatingSystem.IsWindows())`.
- unit3205 compiles to nothing on either build, so its port does nothing and passes on
  every platform; it is still registered, so the case runs and passes like curl's.

A port's own tests pin each platform's answer in its own test (`OSCondition`), so CI on
Linux and macOS checks the branch those platforms take.

## Consequences

- A case is on `PassingUpstreamCases.txt` on every platform: where curl's build runs the
  checks, the port runs Curl's code through them; where it does not, both print a line and
  pass, as the upstream case expects (none of these cases verifies standard output).
- Where Curl gives a wrong answer on a check, the fix lands in the library that holds the
  code, with its own focused unit test (ADR-0482).
