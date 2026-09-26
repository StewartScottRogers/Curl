# Curl.Cli.UnitLibrary

The command-line layer: turning the arguments curl was given into settings, and refusing
them with curl's own messages and exit code when they are wrong. Nothing in the solution
calls this library yet; `Curl.Console` is not wired to it.

## What lives here

| Type | What it does |
| --- | --- |
| `CommandLineParser` | `Parse(arguments)` walks the arguments once and returns a `CommandLineParseResult`. Short options bundle (`-sS`), a value letter takes the rest of its bundle (`-ofile`) or the next argument, long options match exactly and accept `--name=value`, the first `--` ends option parsing, and every other argument is a URL. An empty URL argument is refused as blank; an option's value, empty or not, goes unchanged to the row's applier, which decides whether to refuse it. Stops at the first refusal. |
| `CommandLineOptionTable` | The options the parser recognises, one `CommandLineOption` row each. It holds `--url`, `-s`/`--silent`, `-S`/`--show-error`, `-o`/`--output`, `-d`/`--data`, `-u`/`--user`, `-t`/`--telnet-option`, `--tftp-blksize` and `--tftp-no-options`. `-d`, `-u` and `-t` accept an empty value, as curl does. |
| `CommandLineOption` | One row: long name, optional short letter, whether it takes a value, and the `CommandLineOptionApplier` that checks and sets it. Built with `Flag` (no value), `Text` (non-blank text; an empty value is refused with "blank argument where content is expected") or `Value` (any applier; a numeric option's applier uses `CommandLineNumber`, which refuses an empty value as "expected a proper numerical parameter"). |
| `CommandLineOptionApplier` | The delegate a row uses to write its value into `CommandLineOptions`, or to refuse it. |
| `CommandLineOptions` | The parsed settings: `Urls` (positional and `--url`, in command-line order), `Silent`, `ShowError`, `OutputFiles`, and the ADR-0006 members `PostData` (`-d` as UTF-8 bytes), `Credentials` (`-u` split at the first colon), `TelnetOptions` (every `-t` verbatim, in order), `TftpBlockSize` (unclamped) and `TftpNoOptions`. |
| `CommandLineParseResult` | Either `Options` (when `IsAccepted`) or `Refusal`. |
| `CommandLineRefusal` | The two standard-error lines curl prints for a bad command line, `curl: option <as typed>: <reason>` and the try-help line, with exit code `CurlExitCode.FailedInit` (2). Lines carry no terminator. |
| `CommandLineNumber` | `ParseNonNegative` reads a numeric option value and refuses it as curl does ("expected a proper numerical parameter" / "expected a positive numerical parameter"). `--tftp-blksize` uses it. |
| `UploadUrl` | Completes the URL of a `-T`/`--upload-file` transfer: when the URL path names no file, appends the local file's base name the way curl's `add_file_name_to_url` does (ADR-0004). Pure string work; never reads the file system. |

## How they connect

`CommandLineParser` looks each argument up in `CommandLineOptionTable`, and the row's
applier writes into a fresh `CommandLineOptions`. Any failure becomes a
`CommandLineRefusal`, and the parser returns it inside a `CommandLineParseResult`
without reading further arguments. Adding an option is a new row in
`CommandLineOptionTable` plus the property it sets on `CommandLineOptions`. How an
empty value is refused depends on the value's type, as in curl, so that check lives in
the row's applier and never in the parser.

## What does not live here

- Writing to standard error or choosing the newline: the console layer does that with
  `CommandLineRefusal.StandardErrorLines`.
- `--no-` negation, `-K`/`--config`, `.curlrc`, `--variable` and `--next`: not
  implemented. `--no-silent` is refused as unknown today.
- URL parsing and validation, and opening output files: other layers, after parsing.
