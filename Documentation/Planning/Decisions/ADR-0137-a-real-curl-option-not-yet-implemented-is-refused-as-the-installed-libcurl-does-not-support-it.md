# ADR-0137 — A real curl option Curl does not implement yet is refused as "the installed libcurl version does not support this", apart from a typo

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-496.

## Context

The conformance audit of 2026-09-28 (row 33, Major) counted curl 8.21.0's 280 long options: 138
parsed, 4 refused by design (ADR-0017's `--http2`, `--http2-prior-knowledge`, `--http3` and
`--http3-only`), and 138 unknown to the parser. Until now a real-but-unparsed option and a typo
both printed

```
curl: option --X: is unknown
curl: try 'curl --help' or 'curl --manual' for more information
```

and exited 2, and nothing recorded that as a choice. Since then BL-488 (the nine no-function
options), BL-489 (eight no-effect switches) and BL-491 to BL-495 (`-N`, `--no-clobber`,
`--skip-existing`, `--remove-on-error`, `--out-null`) have moved names out of the unknown pile.

The forces:

- **Complete reimplementation** (Stewart, 2026-09-28): every option any official curl build
  supports is supported on every platform. There is no permanent "refused by design" class;
  ADR-0017's refusals are being superseded (BL-655 for HTTP/2, BL-718 for HTTP/3) and those
  options are implemented by BL-659 and BL-732. So an unimplemented real option is a temporary
  state, and every such option has a task on the board that implements it. On 2026-09-28, after
  BL-488 to BL-495, 93 of the alias table's names still had no `CommandLineOptionTable` row.
- **Drop-in replacement.** A script that passes a real option must not silently get different
  behaviour: refusing is visible, ignoring is not. And whatever Curl prints should be text some
  curl build really prints, so nothing parsing standard error meets a line no curl writes.
- **A typo is not a missing feature.** Telling a user that `--compressed-ssh` "is unknown" says
  they misspelled it; the truth is that this build cannot do it yet.
- **curl already has a line for this.** A curl built without a feature still knows the option and
  refuses it with `the installed libcurl version does not support this`, exit 2 — the line
  ADR-0017 pinned for the Windows build's `--http2`. Measured again on 2026-09-28 against the
  Windows system curl 8.21.0 (Schannel), whose only difference from the mingw reference build is a
  try-help line without `or 'curl --manual'`:

  ```
  > curl --http3 http://127.0.0.1:1/
  curl: option --http3: the installed libcurl version does not support this
  curl: try 'curl --help' for more information                      (exit 2)

  > curl -K cfg http://127.0.0.1:1/          (cfg holds the line "http3")
  curl: <cfg>:1 config file
  curl: option 'http3' the installed libcurl version does not support this
  curl: option -K: the installed libcurl version does not support this
  curl: try 'curl --help' for more information                      (exit 2)

  > curl -K cfg http://127.0.0.1:1/          (cfg holds the line "bogus-opt")
  curl: <cfg>:1 config file
  curl: option 'bogus-opt' is unknown
  curl: option -K: found an unknown config option
  curl: try 'curl --help' for more information                      (exit 2)

  > curl --no-http3 x
  curl: option --no-http3: the given option cannot be reversed with a --no- prefix   (exit 2)
  ```

  (The config-file error line is wrapped at 79 columns with the path in it; `CommandLineRefusal`
  already reproduces that wrapping.)

## Decision

Every long name, short letter and config-file option falls in exactly one of four classes.
`CommandLineParser` tells them apart with two tables and nothing else:
`CommandLineOptionTable` (what Curl parses) and `CurlOptionAliasTable` (every name and letter curl
8.21.0 knows, copied from `tool_getparam.c`). **The list of not-yet-implemented names is derived —
alias table minus option table — and never written by hand**, so it shrinks by itself as each
option gains a row. The refusal lines are built by `CommandLineRefusal`: `UnknownOption` for a
typo and `InstalledLibcurlDoesNotSupport` for a real option not yet implemented.

In the lines below `<spelled>` is the option as typed (the whole argument for a short bundle, as
`UnknownOption` already spells it), and every refusal ends with `CommandLineRefusal.TryHelpLine`.

| Class | Test | Standard error | Exit |
| --- | --- | --- | --- |
| 1. Parsed | Has a row in `CommandLineOptionTable` other than an `UnsupportedFlag` row | Whatever curl prints for it (usually nothing); the option acts. BL-489's no-effect switches are here. | The transfer's |
| 2. Deprecated with no function (BL-488) | A `CommandLineOption` row built by the no-function factory: `--sslv2`/`-2`, `--sslv3`/`-3`, `--metalink`, `--npn`, `--ntlm-wb`, `--egd-file`, `--random-file`, `--krb4`, `--false-start` | `Warning: --<long name> is deprecated and has no function anymore` (from `CommandLineWarning.DeprecatedWithNoFunction`), nothing under `-s`; the transfer runs | The transfer's |
| 3. Real, not yet implemented | In `CurlOptionAliasTable` and either not in `CommandLineOptionTable` or there only as a `CommandLineOption.UnsupportedFlag` row (`--http2`, `--http2-prior-knowledge`, `--http3`, `--http3-only` today) | `curl: option <spelled>: the installed libcurl version does not support this` | 2 |
| 4. Not a curl option | In neither table | `curl: option <spelled>: is unknown` | 2 |

Details of class 3, each chosen to be what a curl built without the feature does:

- **`--no-<name>`**: when the alias table says `<name>` takes no `--no-` prefix, the existing
  `the given option cannot be reversed with a --no- prefix` refusal (as curl says for
  `--no-http3`); otherwise the class-3 line, spelled `--no-<name>`.
- **`--expand-<name>`**: the class-3 line, spelled `--expand-<name>`. The alias table does not
  say whether an unimplemented option takes a value, so the refusal comes before any expansion.
- **A short letter** in a bundle (`-4`, `-6`, `-n` and `-B` on 2026-09-28): the class-3 line,
  ending the bundle as an unknown letter ends it today.
- **A `-K` config line** is treated the same way: `CommandLineRefusal.ConfigFileOptionRefused`
  carries the line's reason through, so the file prints
  `curl: <file>:<line> config file option '<option>' the installed libcurl version does not support this`
  (wrapped at 79 columns), then `curl: option -K: the installed libcurl version does not support this`
  and the try-help line, exit 2. A class-4 line keeps `is unknown` and
  `curl: option -K: found an unknown config option`, as today.
- **`--help <option>`** prints the option's manual section for classes 1 to 3, since curl's help
  and manual list every option (ADR-0069, which already looks names up in the alias table).
  `--help all` and the category pages are curl's own table and list class-3 options too. A class-4
  name gets `CurlOptionManualSection.IncorrectOptionNameMessage`, as today.

ADR-0017's four refused options already print the class-3 line through their `UnsupportedFlag`
rows; they leave class 3 when BL-659 and BL-732 replace those rows with working ones. Class 3 is empty once every alias-table name
has a row; that is the goal, not a limit.

## Consequences

- A user, or a script reading standard error, can tell "misspelled" from "this build cannot do
  that yet", and the text is one real curl builds print, with curl's exit code 2.
- The exit code does not change for any option: both refusals exit 2, as before. No option that
  used to work stops working.
- No list to maintain: adding a `CommandLineOptionTable` row moves a name from class 3 to class 1
  with no other edit. A unit test can pin the invariant that every alias-table name is either
  parsed or refused with the class-3 line.
- Class 3 is visible in the source only as a difference of two tables, so the audit's count (138
  unparsed on 2026-09-28) must be recomputed rather than read; the test above can print it.
- `--expand-<name>` of an unimplemented flag given a value says "does not support this" where a
  real curl with the option would say `variable expansion failure`. Both exit 2; the difference
  lasts only while the option is unimplemented.
- The parser change itself is follow-up work in `Curl.Cli.UnitLibrary`, filed as its own task.

## Alternatives considered

- **Keep `is unknown` for both** (curl's exact bytes for an unknown name). Lost: it tells the user
  they made a typo when they did not, and it makes the unimplemented set invisible, which is why
  the audit flagged it.
- **A new line such as `is not implemented yet`.** Lost: no curl build prints it, so a script
  matching curl's standard error meets text it has never seen; curl already has a line meaning
  "known, but not in this build".
- **Accept the option and ignore it, with or without a warning.** Lost: a script that relies on
  the option would get different behaviour and exit 0 — the one outcome a drop-in replacement must
  never produce.
- **A different exit code (4, `CURLE_NOT_BUILT_IN`).** Lost: curl refuses a known-but-unsupported
  option at parse time with exit 2, as the measurements show.
- **A "refused by design" class for options a platform's curl lacks.** Lost: under the complete
  reimplementation rule every option any official build supports is supported everywhere, so no
  option is permanently refused.
