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

## Testing

Terms used by the test projects. The method is in [Adversarial testing](Adversarial-Testing.md).

| Term | Meaning | Name in code |
| --- | --- | --- |
| Adversarial black-box test | A unit test that attacks a library through its public surface only, with input chosen to break it - a boundary, malformed input, an invalid partition, or an unusual order, timing or concurrency - and checks the answer against an oracle: real curl measured with `Record-CurlExchange.ps1` where the behaviour shows on the command line, otherwise the specification and the library's documented contract. | none (a test written to the method) |

## The audit office

Terms used by the independent audit office under `Audit/`
([ADR-0267](../Planning/Decisions/ADR-0267-an-independent-audit-office-audits-the-dark-factory-from-outside-its-reach.md)),
worded as `Audit/Findings/README.md`, `Audit/Instructions/Report-Format.md` and
`Audit/Scorecards/README.md` word them.

| Term | Meaning | Name in code |
| --- | --- | --- |
| Auditor fingerprint | The hash of the auditor definitions that ran in an audit, recorded in its scorecard so a changed auditor is visible. | none yet; `Audit/Tools/Get-AuditorFingerprint.ps1` is planned by BL-1002 |
| Catch rate | The planted defects an auditor caught over the planted defects assigned to it in one audit. | none (a scorecard column) |
| Finding | One issue an auditor reported, kept as one file `Audit/Findings/AF-####-<slug>.md`; `AF-####` is its ID. | `AF-####` |
| Key | A finding's stable dedupe string, `<auditor>:<where>:<what>:<kind>`, written by the auditor and never containing a line number, so the same issue found again maps to the same finding. | none (a line of the finding) |
| Planted defect | A known defect the seeder puts into the audited tree before an audit, to show whether an auditor would catch it. | none (a catalogue entry) |
| Re-audit | An auditor rerunning a finding's reproduction; a finding closes only when a re-audit confirms the fix. | `reaudits` in an auditor's report |
| Reliable | Said of an auditor in one audit when it caught every planted defect assigned to it, returned a parseable report and did not write to the audited tree; the findings of an auditor that is not reliable are flagged unreliable in that scorecard. | none (a scorecard column) |
| Scorecard | The fixed-format summary of one audit, kept as `Audit/Scorecards/yyyy-MM-dd_HHmm.md`. | none (a file) |
