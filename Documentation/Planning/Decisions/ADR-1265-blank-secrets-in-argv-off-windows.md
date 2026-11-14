# ADR-1265: Blank secrets in argv on Linux and macOS

- Status: Accepted
- Date: 2026-11-13
- Task: BL-5093 (outside fitness review finding STD-048)
- Decided by Claude under Stewart's delegation.

## Context

curl's `cleanarg()` (`tool_getparam.c`) overwrites a secret option value on its own argv with
asterisks of the same length once it has copied it, so `ps` and `/proc/<pid>/cmdline` do not show it.
The review measured curl 8.18.0 blanking the values of `-u`/`--user`, `-U`/`--proxy-user`,
`--oauth2-bearer`, `--pass`, `--proxy-pass`, `-E`/`--cert` and `--proxy-cert`; Curl left them in clear
text. .NET hands `Main` a managed copy of argv, so writing to `args` changes nothing other users see.

## Decision

`Curl.Console`'s `NativeArgumentBlanking` blanks those values in the process's real argv, right after
`RawCommandLineArguments.ForCurrentPlatform` has read the arguments the run parses, so the run keeps its
credentials.

- Which bytes: a pure scan of the arguments against `CommandLineOptionTable.Rows` finds each secret
  value: a separate value whole, an attached one (`-ualice:pw`, `-sSualice:pw`, `--user=alice:pw`)
  past the option, as curl blanks `&parse[1]` and keeps the option letters. A bundle ends at its first
  letter that takes a value or ends bundles; the value of any other option is skipped; `--` stops the
  scan. The arguments line up with the last strings of argv, as `RawCommandLineArguments.Select` does,
  so a `dotnet curl.dll` host works too.
- Linux: argv's strings lie between `arg_start` and `arg_end` (fields 48 and 49 of `/proc/self/stat`);
  they are read and the secret ranges written back with asterisks through `/proc/self/mem`, which a
  process may write for itself. BCL file I/O only, no P/Invoke; a failure leaves argv as it is.
- macOS: `_NSGetArgc`/`_NSGetArgv` from libSystem, as `RawCommandLineArguments` already uses, and the
  bytes are overwritten in place.
- Windows: nothing. curl's Windows build does not blank its command line either (Windows keeps the
  command line in the PEB as UTF-16, which `cleanarg` does not reach).

Only the seven options the review measured are blanked; another option curl may clear
(`--tlspassword`, `--proxy-tlspassword`) can be added to `SecretLongNames` once measured.

## Consequences

The decision of which bytes to blank is fully unit tested on every platform; the two native writers are
thin and excluded from coverage (ADR-0083). A test against the real `/proc` would belong in
`Curl.Console.IntegrationTests` gated to Linux; none is written yet.
