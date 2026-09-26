# Command-line parsing

How `Curl.Cli.UnitLibrary` turns the arguments curl was given into options, or refuses
them with curl's own standard-error lines and exit code. The project's own view is in
`Curl.Cli.UnitLibrary/README.md`; the terms used here are defined in the
[glossary](Glossary.md). Every behaviour below was checked by the tests in
`Curl.Cli.UnitTests` against the local curl 8.21.0 on 2026-09-26, with the option list
per the [curl manpage](https://curl.se/docs/manpage.html).

`Curl.Console` references the project but does not call the parser yet, so nothing in
the shipped executable reads its command line this way today.

## Entry point and result

`CommandLineParser.Parse(arguments)` takes the arguments without the program name and
returns a `CommandLineParseResult`:

| Member | Meaning |
| --- | --- |
| `IsAccepted` | `true` when the command line was accepted and `Options` is set; `false` when it was refused and `Refusal` is set. |
| `Options` | The filled-in `CommandLineOptions`, or `null` when refused. |
| `Refusal` | The `CommandLineRefusal` that stopped parsing, or `null` when accepted. |

`Parse` throws `ArgumentNullException` for a `null` list and never throws otherwise; a
`null` element reads as an empty argument.

## The option table

The parser knows no option by name. It looks every option up in
`CommandLineOptionTable.Rows`, a fixed list of `CommandLineOption` rows. A long name is
matched exactly and case-sensitively, with no prefix matching (`--sil` and `--Silent`
are unknown); a short letter is matched case-sensitively.

Adding an option is one new row in `CommandLineOptionTable` plus the property it sets on
`CommandLineOptions`; the parser does not change. A row is made with one of three
factories:

| Factory | Makes | Empty value |
| --- | --- | --- |
| `CommandLineOption.Flag(longName, shortName, set)` | A flag option; `set` runs when it is given. | Not applicable: a flag takes no value. |
| `CommandLineOption.Text(longName, shortName, set)` | A value option taking text; `set` receives the text. | Refused as `blank argument where content is expected`. |
| `CommandLineOption.Value(longName, shortName, apply)` | A value option whose applier does all of the checking. | Whatever the applier decides. |

The table uses `Value` in two ways. `-d`/`--data`, `-u`/`--user` and
`-t`/`--telnet-option` accept any value, empty included, as curl does. `--tftp-blksize`
reads its value with `CommandLineNumber.ParseNonNegative`, and `--create-file-mode` with
`CommandLineNumber.ParseOctal` (at most octal `0777`); both refuse an empty value as
`expected a proper numerical parameter`, not as blank, because in curl the blank-value
refusal depends on the value's type.

`CommandLineNumber.ParseNonNegative` accepts an optional leading `-` then ASCII digits
only, fitting in an `int`. A malformed or too-large value is
`expected a proper numerical parameter`; a negative value other than `-0` is
`expected a positive numerical parameter`; `-0` reads as zero. Whitespace, `+`,
hexadecimal and fractions are malformed.

## How arguments are read

The parser walks the arguments once, left to right.

| Argument | How it is read |
| --- | --- |
| `--` (the first one) | Ends option parsing; every later argument, including `-o` and `--`, is a URL. |
| `--name` or `--name=value` | A long option. A flag ignores an attached value (`--silent=x`). A value option takes the text after the first `=`, or else the next argument, whatever it looks like (`--output -s` records `-s` as the output file name). |
| `-` alone | Refused as unknown. |
| `-x`, `-xyz` | A short option or a bundle. Each letter is looked up in turn and flags are set; the first value letter takes the rest of the argument as its value (`-ofile`, `-sofile`, and `-os` records the output file `s`), or the next argument, whatever it looks like, when it is the last letter (`-so file`, `-o -s`). |
| Anything else | A positional URL, appended to `Options.Urls`. |

`--url <value>` is a text row: its value is appended to the same `Options.Urls` list as
a positional URL, so the two interleave in command-line order. An empty positional
argument, before or after `--`, is refused as a blank argument with an empty spelled
option.

The parser hands every option value, empty or not, unchanged to the row's applier and
never refuses a value itself.

## Refusals

The first refusal met stops parsing: no later argument is read, and the result carries
that one refusal (`-sS --bogus -o` is refused for `--bogus`, not for the missing `-o`
value). Only when every argument has been read without refusal does the parser check
for a URL, so an unknown option still wins over a missing URL.

Every refusal is two standard-error lines and exit 2 (`CurlExitCode.FailedInit`, curl's
`CURLE_FAILED_INIT`, see [libcurl errors](https://curl.se/libcurl/c/libcurl-errors.html)).
The second line is always the try-help line,
`curl: try 'curl --help' or 'curl --manual' for more information`. The first line is
`curl: option <spelled>: <reason>`, where `<spelled>` is the whole argument as typed
(the whole bundle `-so`, or `--output=` with its `=`), except for the no-URL refusal:

| Factory | First line | When |
| --- | --- | --- |
| `UnknownOption` | `curl: option <spelled>: is unknown` | A long name or a short letter not in the table, or a lone `-`. |
| `RequiresParameter` | `curl: option <spelled>: requires parameter` | A value option is the last argument and has no attached value. |
| `BlankArgument` | `curl: option <spelled>: blank argument where content is expected` | An empty value for a `Text` row, or an empty positional argument (then `<spelled>` is empty: `curl: option : blank argument where content is expected`). |
| `ExpectedProperNumericalParameter` | `curl: option <spelled>: expected a proper numerical parameter` | A numeric value that is malformed, empty or too large for an `int`, or an octal value with a non-octal digit. |
| `ExpectedPositiveNumericalParameter` | `curl: option <spelled>: expected a positive numerical parameter` | A negative decimal value other than `-0`. |
| `TooLargeNumber` | `curl: option <spelled>: too large number` | An octal value past its maximum (`--create-file-mode 1000`). |
| `NoUrlSpecified` | `curl: (2) no URL specified` | A non-empty command line read without refusal that names no URL (`-s`, `--`, `-o file`). |

`CommandLineRefusal.StandardErrorLines` returns the lines without line terminators.
`CommandLineRefusal` writes nothing itself: writing the lines to standard error, and
choosing the newline, is the console layer's job.

## What the parser does not do

- It does not validate URLs or open, create or check any file; those belong to later
  layers.
- It does not implement `-K`/`--config`, `.curlrc`, `--variable` or `--next`.
- An empty command line is accepted with default options. curl 8.21.0 answers it with
  only the try-help line and exit 2; matching that is an open gap (task BL-082).
- `--no-` negation is not implemented: `--no-silent` is refused as unknown today (task
  BL-054).
- It prints no warnings: `Warning: The filename argument '<value>' looks like a flag.`
  for an `-o` value starting with `-` (task BL-051) and
  `Warning: Got more output options than URLs` (task BL-078) are open gaps.
- The largest accepted decimal value is `int.MaxValue`; whether that should match the
  platform curl's ceiling is an open decision (task BL-053).
