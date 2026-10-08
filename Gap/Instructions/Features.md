# Gap analyst method: features

The `gap-features` analyst follows `Gap/Instructions/Analyst-Rules.md` first and this
method second. It reads `<run>/measurements/features.json`, written by
`Gap/Tools/Measure-VersionGap.ps1`, whose header says how each feature is compared.

## What the measurement holds

One item per feature, keyed `features:<name>` as upstream spells it (`Gap-Format.md`
section 3), from `docs/cmdline-opts/version.md` plus any name the reference's `Features:`
line adds. A `gap` means the two `Features:` lines disagree on the name: Curl lacks a
feature the reference (or, with no reference, the documents) lists, or lists one the
reference lacks. Names compare case-insensitively.

The reproduction for a group is the whole tool:

```powershell
powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory <temp>
```

## Grouping

Put every `gap` item in exactly one group:

1. **One group per feature**, keyed `features:<feature-slug>-missing` or
   `features:<feature-slug>-not-in-reference`.
2. **Except features that stand for one capability**, which share a group: for example
   `HTTP2` with `h2c` handling, `GSS-API` with `Kerberos` and `SPNEGO`, `SSL` with
   `TLS-SRP`, `IDN` with `PSL`. Key the group by the capability
   (`features:http2-missing`).

The suggestion names the library that would supply the capability (for example
`Curl.Protocol.Http.UnitLibrary` for `HTTP2`, a hand-built `Curl.<Area>.UnitLibrary` for
one Curl does not have yet) and `Curl.Cli.UnitLibrary`'s `CurlVersionText.FeaturesLine`,
which must list the feature only once Curl serves it.

## Severity

| Gap | Severity |
| --- | --- |
| A feature the reference lists and Curl does not (missing feature) | High |
| Curl lists a feature the reference build lacks; suggest that Curl stop listing it (ADR-0021) | Medium |
| Anything else that differs | Medium |

A group takes the highest severity of its items.

## Example report block

```json
{
  "analyst": "gap-features",
  "area": "features",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "key": "features:gsasl-missing",
      "title": "Feature gsasl is not listed by Curl",
      "severity": "High",
      "introducedIn": null,
      "items": ["features:gsasl"],
      "evidence": "Reference Features: line lists gsasl; Curl's does not. Reproduce: powershell -NoProfile -File Gap\\Tools\\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\\gap",
      "suggestion": "Hand-build SASL mechanisms in a Curl.Sasl.UnitLibrary with its Curl.Sasl.UnitTests, use them from the mail protocols, then add gsasl to CurlVersionText.FeaturesLine in Curl.Cli.UnitLibrary.",
      "touches": ["Curl.Sasl.UnitLibrary", "Curl.Sasl.UnitTests", "Curl.Cli.UnitLibrary", "Curl.Cli.UnitTests"]
    }
  ],
  "notes": ""
}
```
