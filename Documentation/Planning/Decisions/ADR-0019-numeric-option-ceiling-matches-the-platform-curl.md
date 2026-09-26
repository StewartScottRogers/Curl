# ADR-0019 — The ceiling of a numeric option matches the platform curl: 2^31-1 on Windows, 2^63-1 on Linux and macOS

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Stewart (task BL-053, 2026-09-26); recorded by Claude.

## Context

curl 8.21.0 reads most numeric options (`--tftp-blksize`, `--max-redirs`, the seconds of
`--connect-timeout` and `-m`, and the like) into a C `long`. A C `long` is 32 bits on
Windows and 64 bits on Linux and macOS, so the same command line has a different ceiling
depending on the platform:

- Windows (curl 8.21.0, measured 2026-09-26): `--tftp-blksize 99999999999` is refused with
  `curl: option --tftp-blksize: expected a proper numerical parameter`; the largest accepted
  value is 2^31-1 (2147483647).
- Linux and macOS (curl 8.21.0, OpenSSL build): the largest accepted value is 2^63-1
  (9223372036854775807), `LONG_MAX` for a 64-bit `long`.

`-C`/`--continue-at` is different: it reads a `curl_off_t`, which is 64 bits on every
platform, so its ceiling is 2^63-1 everywhere.

BL-037 added `CommandLineNumber.ParseNonNegative` in `Curl.Cli.UnitLibrary`, which reads
into an `int` and so matches Windows on every platform. Curl publishes native binaries for
Windows, Linux and macOS (BL-028), so the ceiling had to be decided: match each platform's
curl, or one ceiling everywhere.

## Decision

Curl matches the platform curl it replaces:

| Platform | Ceiling of a `long`-valued numeric option | Matches |
| --- | --- | --- |
| Windows | 2^31-1 (2147483647) | curl 8.21.0 on Windows, 32-bit `long` |
| Linux | 2^63-1 (9223372036854775807) | curl 8.21.0, OpenSSL build, 64-bit `long` |
| macOS | 2^63-1 (9223372036854775807) | curl 8.21.0, OpenSSL build, 64-bit `long` |

A value above the ceiling is refused as upstream refuses it on that platform, with
`expected a proper numerical parameter`. Limits curl derives from the `long` ceiling, such
as the most whole seconds `--connect-timeout` and `-m` accept (`LONG_MAX / 1000`, less
one), follow the same platform rule.

`-C`/`--continue-at` reads a 64-bit value (ceiling 2^63-1) on every platform regardless,
through its own reader (`CommandLineNumber.ParseOffset`), never through the `int`/`long`
option reader.

## Consequences

- A script that works against the platform's curl works against Curl on that platform,
  including edge values near the ceiling: the drop-in promise holds.
- The same command line can be accepted on Linux and refused on Windows. That is upstream
  behaviour, reproduced on purpose.
- `CommandLineNumber` needs a platform-dependent ceiling and a 64-bit result type where the
  value can exceed `int`; the consumers of those values widen with it. Filed as BL-257.
- Tests must cover both ceilings on one host, so the ceiling is a choice the tests can
  make, not only a compile-time or run-time platform check they cannot reach.

## Alternatives considered

- **2^31-1 everywhere** (today's code). Simplest, but refuses values Linux and macOS curl
  accept, a visible divergence from upstream on two of the three platforms.
- **2^63-1 everywhere.** Accepts values Windows curl refuses, so a script relying on the
  Windows refusal (exit code 2) behaves differently: a divergence on the platform this
  project measures against most.
