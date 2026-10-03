# ADR-0378 — An unknown `--ech` mode fails the transfer with exit 43, as curl's ECH build does

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

`curl --ech <mode>` has three answers for a mode libcurl does not know, depending on the build
(BL-1107 and BL-1172 Notes, measured 2026-10-02):

- curl 8.21.0 built with ECH (the BL-1107 Docker build) parses the value unchecked, then libcurl's
  `setopt_ech` refuses it when the tool sets `CURLOPT_ECH` (option 10325, 0x2855): exit 43,
  `curl: (43) setopt 0x2855 got bad argument`, with or without an `ecl:` list beside it.
- The platform builds without ECH (mingw Schannel 8.21.0, WSL's OpenSSL 8.18.0) refuse every
  `--ech` value, valid or not, while parsing: `curl: option --ech: the installed libcurl version
  does not support this`, the `curl --help` hint, exit 2.

`setopt_ech` (curl 8.21.0 `lib/setopt.c`) accepts `false`, `grease`, `true` and `hard` with
`strcmp`, a value longer than four characters starting `ecl:` and one longer than three starting
`pn:` with `strncmp`; anything else is `CURLE_BAD_FUNCTION_ARGUMENT`. The comparisons are
case-sensitive, so `TRUE` and `PN:x` are refused even though the tool's own `pn:`/`ecl:` split
ignores case.

## Decision

- Curl answers as the ECH build does. ADR-0327 makes `--ech` work on every platform, so Curl is an
  ECH build everywhere, and the platform builds' exit 2 would refuse the valid modes too.
- `Curl.Cli`'s parser still records the mode unchecked, as curl's does.
  `CommandLineOptions.EchModeIsMalformed` says when libcurl would refuse it, by `setopt_ech`'s rules.
- `Curl.Console` refuses such a transfer where it refuses a malformed `--interface` (BL-600): a
  `-v` line `* setopt 0x2855 got bad argument`, then `curl: (43) setopt 0x2855 got bad argument`,
  the `-w` output still written, and no later URL transferred. The `-v` line and the `-w` output
  were not measured with the ECH build; they follow the `--interface` measurement, since both are
  the same `config2setopts` failure path in curl's tool. A malformed `--interface` is checked first.

## Consequences

`--ech bogus` now fails as curl's ECH build does, before any connection, instead of quietly running
without ECH. A script written against a non-ECH platform curl sees exit 43 rather than exit 2; that
difference already exists for every valid `--ech` value under ADR-0327.

## Alternatives considered

- **Exit 2 at parse time, as the platform builds do.** Lost: it would also have to refuse
  `--ech true`, undoing ADR-0327.
- **Case-insensitive keywords.** Lost: `setopt_ech` uses `strcmp`; matching it is the drop-in rule.
