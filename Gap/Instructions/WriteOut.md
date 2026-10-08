# Gap analyst method: write-out

The `gap-writeout` analyst follows `Gap/Instructions/Analyst-Rules.md` first and this
method second. It reads `<run>/measurements/writeout.json`, written by
`Gap/Tools/Measure-WriteOutGap.ps1`, whose header says how each variable is probed.

## What the measurement holds

Two facets per variable from `docs/cmdline-opts/write-out.md` (`Gap-Format.md` section 3):

| Key | A gap means |
| --- | --- |
| `writeout:<name>` | Curl does not recognise the variable: it prints the unknown-variable warning the reference does not (or, with no reference, any documented variable draws the warning). |
| `writeout:<name>:value` | Both recognise it, but standard output differs byte for byte. Volatile variables have no value facet. |

A function-style form such as `%header{}` is keyed by its name (`writeout:%header{}`).
Each item's `evidence` holds the command line; the reproduction is that command line or
the whole tool:

```powershell
powershell -NoProfile -File Gap\Tools\Measure-WriteOutGap.ps1 -OutFile <temp>\writeout.json
```

## Grouping

Put every `gap` item in exactly one group:

1. **Unknown variables, one group per Curl component that would supply them**, so one
   task can close them all: for example the TLS layer's certificate variables, the
   connection's address and port variables, the transfer's size and count variables, the
   timing variables. Key them `writeout:unknown-<component>-variables`.
2. **Value differences, one group per variable**, keyed `writeout:<variable>-value`.

`Curl.Output.UnitLibrary` writes the `-w` variables (`WriteOutTemplateRenderer`,
`TransferWriteOutVariables`, `IWriteOutVariableSource`, `WriteOutJson`); read it to find
where each cause lives. Every suggestion names `Curl.Output.UnitLibrary` and, for an
unknown variable, the library that would supply its value.

## Severity

| Gap | Severity |
| --- | --- |
| `writeout:<name>`: the variable is unknown to Curl (missing variable) | High |
| `writeout:<name>:value`: the value differs | Medium |

A group takes the highest severity of its items. A value difference on a common path
(a plain GET's `%{http_code}` or `%{size_download}`) is Critical, per `Analyst-Rules.md`
rule 5.

## Example report block

```json
{
  "analyst": "gap-writeout",
  "area": "writeout",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "key": "writeout:unknown-timing-variables",
      "title": "Write-out variable time_queue is unknown to Curl",
      "severity": "High",
      "introducedIn": "8.12.0",
      "items": ["writeout:time_queue"],
      "evidence": "curl -s -o NUL -w %{time_queue} file:///.../x: expected no warning, actual 'curl: unknown --write-out variable: 'time_queue''. Reproduce: powershell -NoProfile -File Gap\\Tools\\Measure-WriteOutGap.ps1 -OutFile $env:TEMP\\writeout.json",
      "suggestion": "Record the time a transfer waits in the queue and expose it as time_queue in TransferWriteOutVariables in Curl.Output.UnitLibrary, formatted like the other time_ variables.",
      "touches": ["Curl.Output.UnitLibrary", "Curl.Output.UnitTests"]
    }
  ],
  "notes": ""
}
```
