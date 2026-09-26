# Glossary

One term, one meaning, one name in code. When code or a document uses one of these
terms, it means exactly what is written here; when a concept below needs naming in code,
it uses the name in the "Name in code" column.

## The command line

Terms used by `Curl.Cli.UnitLibrary`. How they fit together is in
[Command-line parsing](Command-Line-Parsing.md).

| Term | Meaning | Name in code |
| --- | --- | --- |
| Applier | The delegate a value option's row uses to check its value and write it into the options, or to refuse it. The parser hands it every value unchanged, empty included. | `CommandLineOptionApplier`, `CommandLineOption.Apply` |
| Blank argument | An empty string where content is expected: an empty positional argument, or an empty value for a row built with `CommandLineOption.Text` (`-o ""`, `--output=`, `--url ""`). It is refused with the reason `blank argument where content is expected`. | `CommandLineRefusal.BlankArgument` |
| Bundle | One argument of a single `-` followed by several short letters, such as `-sS`. Each letter is looked up in turn; the first value letter takes the rest of the bundle as its value (`-sofile`), or the next argument when it is the last letter. | none (handled inside `CommandLineParser`) |
| Flag option | An option that takes no value; giving it sets a `bool` on the options. A value attached to a long flag (`--silent=x`) is ignored, as curl does. | `CommandLineOption.Flag`, `TakesValue == false` |
| Option row | One entry of the option table: long name, optional short letter, whether it takes a value, and its applier. | `CommandLineOption` |
| Option table | The fixed list of option rows the parser recognises. It holds the option definitions, not the parsed settings. | `CommandLineOptionTable.Rows` |
| Options | The settings one command line asks for, filled in by the rows' appliers: URLs, output files, flags and values. Not to be confused with the option table, which defines what can be asked for. | `CommandLineOptions` |
| Refusal | The outcome of a command line curl will not run: the two standard-error lines (a first line naming the problem, then the try-help line) and exit 2 (`CurlExitCode.FailedInit`, curl's `CURLE_FAILED_INIT`). A refusal writes nothing itself. | `CommandLineRefusal` |
| Spelled option | The whole argument exactly as the user typed it, which a refusal quotes: `--output=`, `-so`, `--bogus=x`, or empty for a blank positional argument. It is not the option's canonical name. | `spelledOption` parameters |
| Try-help line | The second standard-error line of every refusal, `curl: try 'curl --help' or 'curl --manual' for more information`. | `CommandLineRefusal.TryHelpLine` |
| Value option | An option that takes a value, attached (`-ofile`, `--output=file`) or as the next argument. Built with `CommandLineOption.Text`, which refuses an empty value as blank, or with `CommandLineOption.Value`, whose applier decides what an empty value means. | `CommandLineOption.Text`, `CommandLineOption.Value`, `TakesValue == true` |
