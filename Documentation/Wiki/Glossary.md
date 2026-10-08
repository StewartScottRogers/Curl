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
| Integration test | A test that touches something real outside the process: a socket it sends or receives bytes on, a file or device it did not create, the OS, a native API or a system agent. It carries `[TestCategory("Integration")]`, lives only in an IntegrationTests project, and is skipped by the fast run. Files in a temporary directory the test creates and deletes, and a loopback socket opened and closed without sending a byte, do not make a test an Integration test ([ADR-0421](../Planning/Decisions/ADR-0421-integration-tests-live-only-in-integrationtests-projects.md)). | `TestCategory("Integration")` |
| IntegrationTests project | The test project that holds an area's Integration tests and nothing else, named `Curl.<Area>.IntegrationTests` and listed in `Curl.slnx` immediately before its library. A `*.UnitTests` project holds no Integration test; coverage is measured without IntegrationTests projects ([ADR-0421](../Planning/Decisions/ADR-0421-integration-tests-live-only-in-integrationtests-projects.md)). | `Curl.<Area>.IntegrationTests` |
| LongRunning test | A test that touches nothing outside the process but takes longer than the 3-second `SLOW:` budget, such as RFC 7748's million-iteration vectors. It stays in its `*.UnitTests` project, carries `[TestCategory("LongRunning")]` and a condition attribute that skips it unless `CURL_RUN_LONG_RUNNING_TESTS` is `1` ([ADR-0421](../Planning/Decisions/ADR-0421-integration-tests-live-only-in-integrationtests-projects.md)). | `TestCategory("LongRunning")` |

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

## The gap analysis office

Terms used by the gap analysis office under `Gap/`
([ADR-0433](../Planning/Decisions/ADR-0433-a-gap-analysis-office-measures-curl-against-upstream-curl-releases.md)),
worded as `Gap/Instructions/Gap-Format.md` words them.

| Term | Meaning | Name in code |
| --- | --- | --- |
| Analyst | One of the seven read-only agents, one per area, that reads a run's measurement of its area, groups the gaps by cause, suggests where in Curl to close each cause, and ends with one report block. | `.claude/agents/gap-<area>.md`; the report is `<run>/reports/gap-<area>.md` |
| Excluded | The state of an item that does not apply, with a reason (for example `reference-lacks:<feature>`, `platform:<os>`, `debug-build-only`); it counts in neither X nor Y. | `excluded` (`items[].state`) |
| Gap | The state of an item Curl does not match; it counts in Y but not in X. | `gap` (`items[].state`) |
| Gap finding | One cause of one or more gaps, kept as one file `Gap/Findings/GF-####-<slug>.md`, filed `open` (accepted), closed only when a run measures every one of its items as match or excluded. | `GF-####`; written by `Gap/Tools/Write-GapFindings.ps1` |
| Item | One thing upstream has in one area - an option, scheme, feature, write-out variable, exit code, environment variable or config element, or test case - listed in the upstream inventory and given one state per measurement. | `items[]` |
| Item key | An item's stable name, `<area>:<item>[:<facet>]` (for example `options:--ech:argument`), the same in every file, run and version and never holding a value, date or line number. | `items[].key` |
| Match | The state of an item where Curl behaves as upstream does; it counts in X and Y. | `match` (`items[].state`) |
| Measurement | One area's result in one run: every item of the targeted version's inventory with exactly one state on one platform, and the counts X and Y. | `<run>/measurements/<area>.json`, written by the area's `Gap/Tools/Measure-*` tool |
| Newest version | The latest upstream curl release, recorded by the weekly release watcher. | `version` in `Gap/Baselines/newest.json` |
| Reference build | The real curl a probe compares Curl with: Git for Windows' Schannel mingw curl on Windows, the curl on `PATH` elsewhere, used only when its `--version` names the targeted version; otherwise the measurement falls back to the release's documents. | `Get-GapReferenceCurl` in `Gap/Tools/Invoke-GapProbe.ps1` |
| Regression | A closed gap finding whose item measures gap again; it reopens with `regression: true`. | `regression` (finding front matter) |
| Release diff | The item-by-item comparison of two upstream versions' inventories: what was added, removed or changed. | `<run>/measurements/release-<version>.json`, written by `Gap/Tools/Compare-UpstreamReleases.ps1` |
| Scope | Whether a gap finding is against the targeted version (`target`, gets tasks) or only against the newest (`newest`, waits for a retarget ADR). | `scope` (finding front matter) |
| Targeted version | The upstream curl release Curl matches and every measurement is made against; only an ADR moves it. | `version` in `Gap/Baselines/target.json` |
| Unmeasured | The state of an item the office cannot measure yet, with a reason (for example `needs-server:<protocol>`, `no-probe`); it counts in Y but not in X, so it lowers the score. | `unmeasured` (`items[].state`) |
| Upstream inventory | What upstream has in one area for one version, read from the release tarball; it holds no measurement. | `Gap/Upstream/<version>/<area>.json` |
