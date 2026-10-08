# Gap analyst method: options

The `gap-options` analyst follows `Gap/Instructions/Analyst-Rules.md` first and this method
second. It reads `<run>/measurements/options.json`, written by
`Gap/Tools/Measure-OptionGap.ps1`, whose header says how each facet is probed.

## What the measurement holds

One item per option facet, keyed as `Gap-Format.md` section 3 says:

| Key | Probe | A gap means |
| --- | --- | --- |
| `options:--<long>` | `curl --<long> [placeholder]`, no URL | Curl does not recognise the option, or answers it differently. |
| `options:--<long>:alias` | `curl -<short> [placeholder]` | The short alias is missing or answers differently. |
| `options:--<long>:argument` | `curl --<long>` with nothing after it | The missing-argument answer differs. |
| `options:--<long>:no-form` | `curl --no-<long>` | The `--no-` form is missing or answers differently. |

Each item's `expected` and `actual` hold the exit code and standard error of the reference
and of Curl; its `evidence` holds the command line. The reproduction for a group is that
command line run through `Gap/Tools/Invoke-GapProbe.ps1`, or the whole tool:

```powershell
powershell -NoProfile -File Gap\Tools\Measure-OptionGap.ps1 -OutFile <temp>\options.json
```

## Grouping

Work in this order, and put every `gap` item in exactly one group:

1. **Unknown options, by parsing component.** Options Curl refuses as unknown (Curl's
   `actual` says the option is unknown, the reference's `expected` does not) are grouped
   by the Curl component that would parse them, so one task can close them all. Options
   that need only an entry in `Curl.Cli.UnitLibrary`'s `CommandLineOptionTable` and a field
   in `CommandLineOptions` share a group per protocol or feature family (for example
   `options:unknown-tls-options`, `options:unknown-ftp-options`), named from the option's
   `protocols` attribute in the upstream inventory. A family with more than about twenty
   options is split by protocol, so the task stays one `/task-run`.
2. **Facets of one option, by shared cause.** The facets of an option Curl already knows go
   in one group when they share a cause: for example an option whose alias, argument and
   `--no-` form all fail because its table entry is wrong is one group, keyed by the option
   (`options:<long>-table-entry`).
3. **One message, many options.** Facets that fail the same way across options (every
   `:argument` facet prints a different missing-argument message, every `:no-form` of a
   kind is refused) are one group keyed by the message or rule
   (`options:missing-argument-message`, `options:no-form-refused`), not one per option.
4. **The rest**, one group per remaining cause.

Read `Curl.Cli.UnitLibrary` (`CommandLineParser`, `CommandLineOptionTable`,
`CommandLineOptions`, `CommandLineOptionApplier`, `CurlOptionAliasTable`) to find where
each cause lives before writing its suggestion.

## Severity

| Gap | Severity |
| --- | --- |
| A different exit code or output for an option on a common path: `-o`, `-L`, `-u`, `-d`/`--data` (POST), or a plain GET's defaults | Critical |
| `options:--<long>` where Curl does not know the option at all | High |
| `:alias`, `:argument`, `:no-form`, or a known option whose message text differs | Medium |
| Anything else | Low |

A group takes the highest severity of its items.

## Suggestions

Name where `Curl.Cli.UnitLibrary` parses the option (the table entry, the parser branch or
the applier method) and, for a new option, the library that carries out its effect, with the
upstream document `docs/cmdline-opts/<long>.md`. Every options suggestion ends with: "Keep
`--ai-help` right for the options this changes (CLAUDE.md)." `touches` names
`Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`, plus each library that carries the effect
and its `.UnitTests` project.

## Example report block

```json
{
  "analyst": "gap-options",
  "area": "options",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "key": "options:encrypted-client-hello",
      "title": "Encrypted Client Hello option --ech is unknown to Curl",
      "severity": "High",
      "introducedIn": "8.8.0",
      "items": ["options:--ech", "options:--ech:argument"],
      "evidence": "curl --ech x: expected 'curl: no URL specified' (exit 2), actual 'curl: option --ech: is unknown' (exit 2). Reproduce: powershell -NoProfile -File Gap\\Tools\\Measure-OptionGap.ps1 -OutFile $env:TEMP\\options.json",
      "suggestion": "Add --ech to CommandLineOptionTable and CommandLineOptions in Curl.Cli.UnitLibrary, with its four argument forms from docs/cmdline-opts/ech.md, and pass it to the TLS layer. Keep --ai-help right for the options this changes (CLAUDE.md).",
      "touches": ["Curl.Cli.UnitLibrary", "Curl.Cli.UnitTests"]
    }
  ],
  "notes": ""
}
```
