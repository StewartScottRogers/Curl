# ADR-0039 — A 64-bit numeric option value saturates where CommandLineOptions holds less

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (task BL-257, 2026-09-26).

## Context

[ADR-0019](ADR-0019-numeric-option-ceiling-matches-the-platform-curl.md) makes the ceiling of a
`long`-valued numeric option follow the platform curl: 2^31-1 on Windows, 2^63-1 on Linux and
macOS. BL-257 widened `CommandLineNumber.ParseNonNegative`, `ParseMinusOneOrMore` and
`ParseSeconds` in `Curl.Cli.UnitLibrary` to read up to 2^63-1 where the platform allows it.
Measured against the OpenSSL curl 8.18.0 in WSL (Ubuntu) on 2026-09-26, `--tftp-blksize` and
`--max-redirs` accept `9223372036854775807`, and `--connect-timeout` and `-m` accept
`9223372036854774` whole seconds (`CURLOPT_TIMEOUT_MS` 9223372036854774000).

The values those readers produce are carried on by types that hold less:
`CommandLineOptions.TftpBlockSize` and `ITransferContext.TftpBlockSize` are `int?`,
`CommandLineOptions.MaxRedirects` and `RedirectPolicy.MaxRedirects` are `int`, and the two
timeouts are `TimeSpan?`, whose longest value is about 922337203685 seconds (29,000 years).
Widening those types reaches `Curl.Console`, `Curl.Core.UnitLibrary` and the shared contract
`Curl.Protocol.Abstractions.UnitLibrary`, which other tasks hold.

## Decision

The readers return the full 64-bit value; `CommandLineOptionTable` saturates it where it is
recorded:

| Option | Recorded as | Why the result is the same as curl's |
| --- | --- | --- |
| `--tftp-blksize` | `Math.Min(value, int.MaxValue)` | The TFTP handler clamps the block size to 8-65464, as curl does, so every value past 65464 asks for 65464. |
| `--max-redirs` | `Math.Min(value, int.MaxValue)` | No transfer follows 2^31-1 redirects, so either limit is never reached. |
| `--connect-timeout`, `-m` | at most the longest whole milliseconds a `TimeSpan` holds | No transfer runs for 29,000 years, so either limit never fires. |

Acceptance and refusal are unchanged by the saturation: a value is accepted or refused by the
platform ceiling, exactly as the platform curl accepts or refuses it.

## Consequences

- `CommandLineOptions`, `ITransferContext` and `RedirectPolicy` keep their types; no other
  project changes.
- On Linux and macOS a timeout can now exceed what .NET timers accept, about 49.7 days
  (`uint.MaxValue - 1` milliseconds): `CancellationTokenSource`, `CancelAfter` and `Task.Delay`
  throw past it. Code that waits on `MaxTime` or `ConnectTimeout` caps the wait it hands a
  timer, as `TftpTimeLimits` caps it with its resend time; BL-174 carries that note.
- A limit that would show its own value, such as `Maximum (N) redirects followed`, would print
  2147483647 in place of a larger limit, but only after that many redirects, which no transfer
  reaches.

## Alternatives considered

- **Widen `CommandLineOptions` and every consumer to `long`.** Carries the exact value, but
  changes a shared contract for no observable difference, and reaches projects other tasks hold.
- **Refuse values past what the consumer holds.** Diverges from the platform curl, which
  accepts them.
