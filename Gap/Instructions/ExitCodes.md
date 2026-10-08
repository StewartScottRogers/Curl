# Gap analyst method: exit codes

The `gap-exitcodes` analyst follows `Gap/Instructions/Analyst-Rules.md` first and this
method second. It reads `<run>/measurements/exitcodes.json`, written by
`Gap/Tools/Measure-ExitCodeGap.ps1`, whose header says how each code is compared.

## What the measurement holds

The comparison is with the release's text, not a binary (`referenceFallback` is `docs`).
Keys per code (`Gap-Format.md` section 3):

| Key | A gap means |
| --- | --- |
| `exitcodes:<n>` | No `CurlExitCode` member has the number `n`. |
| `exitcodes:<n>:strerror` | Curl's `curl_easy_strerror` text for the code differs from `lib/strerror.c`'s, or no member has the number. |
| `exitcodes:<n>:man` | Has the state of `exitcodes:<n>`: the curl tool documents code `n`, and Curl lacks it. |

The reproduction for a group is the whole tool:

```powershell
powershell -NoProfile -File Gap\Tools\Measure-ExitCodeGap.ps1 -OutFile <temp>\exitcodes.json
```

## Grouping

Put every `gap` item in exactly one group. There are two groups at most:

1. **Missing codes**, key `exitcodes:missing-codes`: every `exitcodes:<n>` and
   `exitcodes:<n>:man` gap, and every `exitcodes:<n>:strerror` gap whose code is missing
   too. The suggestion adds each member to `CurlExitCode` in
   `Curl.Protocol.Abstractions.UnitLibrary`, under upstream's `CURLE_` name and number, and
   its text to `Curl.Console/CurlEasyErrorText.cs`.
2. **Text differences**, key `exitcodes:strerror-text`: every `exitcodes:<n>:strerror` gap
   whose code exists. The suggestion corrects each entry in
   `Curl.Console/CurlEasyErrorText.cs` to `strerror.c`'s text, quoting both.

## Severity

| Gap | Severity |
| --- | --- |
| A code Curl lacks (missing code) | High |
| A `curl_easy_strerror` text that differs | Medium |

A group takes the highest severity of its items.

## Example report block

```json
{
  "analyst": "gap-exitcodes",
  "area": "exitcodes",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "key": "exitcodes:missing-codes",
      "title": "Exit code 101 (CURLE_ECH_REQUIRED) is missing from CurlExitCode",
      "severity": "High",
      "introducedIn": null,
      "items": ["exitcodes:101", "exitcodes:101:man", "exitcodes:101:strerror"],
      "evidence": "libcurl-errors.md lists CURLE_ECH_REQUIRED (101); no CurlExitCode member has 101. Reproduce: powershell -NoProfile -File Gap\\Tools\\Measure-ExitCodeGap.ps1 -OutFile $env:TEMP\\exitcodes.json",
      "suggestion": "Add EchRequired = 101 to CurlExitCode in Curl.Protocol.Abstractions.UnitLibrary and its strerror.c text to Curl.Console/CurlEasyErrorText.cs.",
      "touches": ["Curl.Protocol.Abstractions.UnitLibrary", "Curl.Protocol.Abstractions.UnitTests", "Curl.Console", "Curl.Console.UnitTests"]
    }
  ],
  "notes": ""
}
```
