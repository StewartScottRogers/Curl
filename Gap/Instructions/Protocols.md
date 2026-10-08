# Gap analyst method: protocols

The `gap-protocols` analyst follows `Gap/Instructions/Analyst-Rules.md` first and this
method second. It reads `<run>/measurements/protocols.json`, written by
`Gap/Tools/Measure-VersionGap.ps1`, whose header says how each scheme is probed.

## What the measurement holds

One item per scheme, keyed `protocols:<scheme>` in lower case (`Gap-Format.md` section 3),
from `docs/cmdline-opts/_PROTOCOLS.md` plus any scheme the reference's `Protocols:` line adds.

| A `gap` means | Read from |
| --- | --- |
| Curl's `curl -V` does not list a scheme the reference (or, with no reference, the documents) lists | `expected` and `actual` disagree on listing it |
| Both list the scheme, but `<scheme>://127.0.0.1:1/` exits with a different code | the two exit codes |
| Curl lists a scheme the reference build lacks | `expected` does not list it, `actual` does |

The reproduction for a group is the whole tool:

```powershell
powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory <temp>
```

## Grouping

Put every `gap` item in exactly one group:

1. **One group per scheme family and cause.** A plain scheme and its TLS variant share a
   group: `ftp` and `ftps`, `imap` and `imaps`, `ldap` and `ldaps`, `ws` and `wss`, `scp`
   and `sftp`, and so on. Key it by the family, `protocols:<family>-<cause>`, for example
   `protocols:mqtt-missing`, `protocols:ftp-exit-code`.
2. **Schemes Curl lists but the reference lacks** are one group per family keyed
   `protocols:<family>-not-in-reference`, kept apart from missing schemes because the fix
   is the opposite.

The suggestion names the family's `Curl.Protocol.<Name>.UnitLibrary` (existing ones:
Dict, File, Ftp, Gopher, Http, Imap, Ldap, Mqtt, Pop3, Rtsp, Smb, Smtp, Ssh for `scp` and
`sftp`, Telnet, Tftp, Ws). A scheme with no library yet names the new
`Curl.Protocol.<Name>.UnitLibrary` and its `.UnitTests` twin. A change to what `curl -V`
lists also names `Curl.Cli.UnitLibrary`'s `CurlVersionText.ProtocolsLine`.

## Severity

| Gap | Severity |
| --- | --- |
| A scheme the reference lists and Curl does not (missing scheme) | High |
| Both list it, but the exit code differs | Medium |
| Curl lists a scheme the reference build lacks; suggest that Curl stop listing it (ADR-0021) | Medium |

A group takes the highest severity of its items.

## Example report block

```json
{
  "analyst": "gap-protocols",
  "area": "protocols",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "key": "protocols:rtmp-missing",
      "title": "RTMP schemes rtmp and rtmps are missing from Curl",
      "severity": "High",
      "introducedIn": null,
      "items": ["protocols:rtmp", "protocols:rtmps"],
      "evidence": "Reference Protocols: line lists rtmp and rtmps; Curl's does not. Reproduce: powershell -NoProfile -File Gap\\Tools\\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\\gap",
      "suggestion": "Add a Curl.Protocol.Rtmp.UnitLibrary with its Curl.Protocol.Rtmp.UnitTests serving rtmp and rtmps behind IConnection, and list both in CurlVersionText.ProtocolsLine in Curl.Cli.UnitLibrary.",
      "touches": ["Curl.Protocol.Rtmp.UnitLibrary", "Curl.Protocol.Rtmp.UnitTests", "Curl.Cli.UnitLibrary", "Curl.Cli.UnitTests"]
    }
  ],
  "notes": ""
}
```
