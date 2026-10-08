# Gap analyst method: environment

The `gap-environment` analyst follows `Gap/Instructions/Analyst-Rules.md` first and this
method second. It reads `<run>/measurements/environment.json`, written by
`Gap/Tools/Measure-EnvironmentGap.ps1`, whose header lists every recipe.

## What the measurement holds

| Key | Kind | A gap means |
| --- | --- | --- |
| `environment:<NAME>` | variable | Curl reads the variable differently: exit code, stdout, stderr or, for a proxy variable, the request bytes the loopback proxy received. |
| `environment:config-path:<n>` | config-path | Curl does not read, or reads differently, a config file in location `<n>` of `docs/cmdline-opts/config.md`. |
| `environment:config-syntax:<slug>` | config-syntax | Curl parses one config-file syntax rule differently. |

Each item's `expected` and `actual` hold the reference's and Curl's answers; `evidence`
names the recipe. The reproduction for a group is the tool itself:

```powershell
powershell -NoProfile -File Gap\Tools\Measure-EnvironmentGap.ps1 -OutFile <temp>\environment.json
```

## Grouping, by mechanism

Every `gap` item goes in exactly one group, chosen by the mechanism that reads it:

1. **Proxy variables** (`http_proxy`, `HTTPS_PROXY`, `ALL_PROXY`, `NO_PROXY` and their
   case forms): one group per cause, keyed `environment:proxy-<cause>`. Curl picks the
   proxy in `Curl.Core.UnitLibrary`'s `ProxySelector` and matches `NO_PROXY` in
   `NoProxyMatcher`.
2. **Config-file locations** (`config-path:<n>`): one group, `environment:config-file-search`,
   unless the gaps have plainly different causes. Curl searches in `Curl.Cli.UnitLibrary`'s
   `DefaultConfigFileSearch` and `AccountHomeDirectory`.
3. **Config syntax** (`config-syntax:<slug>`): one group, `environment:config-syntax`,
   unless the gaps have plainly different causes. Curl parses config lines in
   `Curl.Cli.UnitLibrary`'s `ConfigFileSyntax` and `ConfigFileLine` and applies them in
   `ConfigFileApplier`.
4. **Other variables**: one group per variable or per shared cause, keyed
   `environment:<variable-lowercase>` or by the cause.

### Unmeasured items are notes, never groups

Items the measurement marks `unmeasured` (reason `no-probe` or `needs-server:<protocol>`)
are listed in the report's `notes` as recipes worth adding to
`Measure-EnvironmentGap.ps1`, one line each: the key and what the recipe would set and
compare. They are never made into groups, because only measured gaps become findings.
`excluded` items are neither grouped nor noted.

## Severity

| Gap | Severity |
| --- | --- |
| A proxy variable or config file changes the exit code or output bytes of a plain GET, a POST, `-o`, `-L` or `-u` | Critical |
| Curl ignores a variable or config location outright | High |
| A variable, location or syntax rule is read but behaves differently | Medium |
| Anything else | Low |

A group takes the highest severity of its items.

## Suggestions

Name the type that reads the mechanism (above) and the upstream document section:
`docs/cmdline-opts/_ENVIRONMENT.md` or `docs/libcurl/libcurl-env.md` for a variable, and
`docs/cmdline-opts/config.md` for the config file. `touches` names that library and its
`.UnitTests` project.

## Example report block

```json
{
  "analyst": "gap-environment",
  "area": "environment",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "key": "environment:proxy-no-proxy-ignored",
      "title": "NO_PROXY does not bypass the proxy set by http_proxy",
      "severity": "Medium",
      "introducedIn": null,
      "items": ["environment:NO_PROXY"],
      "evidence": "Recipe NO_PROXY=gap.invalid with http_proxy=<loopback proxy>, curl -s http://gap.invalid/: expected no request at the proxy (exit 6), actual 'GET http://gap.invalid/ HTTP/1.1' at the proxy (exit 0). Reproduce: powershell -NoProfile -File Gap\\Tools\\Measure-EnvironmentGap.ps1 -OutFile $env:TEMP\\environment.json",
      "suggestion": "Make ProxySelector in Curl.Core.UnitLibrary consult NoProxyMatcher with the NO_PROXY variable before using http_proxy, as docs/cmdline-opts/_ENVIRONMENT.md (NO_PROXY) describes.",
      "touches": ["Curl.Core.UnitLibrary", "Curl.Core.UnitTests"]
    }
  ],
  "notes": "Recipes worth adding: environment:CURL_CA_BUNDLE (set it to a temporary bundle, compare the TLS verify result against a loopback TLS server)."
}
```
