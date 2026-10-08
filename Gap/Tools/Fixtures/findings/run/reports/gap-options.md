Analysis of the options area. An earlier draft block, superseded:

```json
{ "analyst": "gap-options", "area": "options", "run": "draft", "groups": [], "notes": "draft" }
```

Final report block:

```json
{
  "analyst": "gap-options",
  "area": "options",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "title": "Encrypted Client Hello options are missing",
      "severity": "High",
      "introducedIn": "8.8.0",
      "items": ["options:--ech", "options:--ech:argument"],
      "evidence": "curl --ech true https://localhost:8443/ exits 0 on the reference and 2 on Curl.",
      "suggestion": "Parse --ech and its four argument forms in Curl.Console and pass them to the TLS layer.",
      "touches": ["Curl.Console", "Curl.Console.UnitTests"]
    },
    {
      "title": "Fail option is missing",
      "severity": "High",
      "introducedIn": null,
      "items": ["options:--fail", "options:--fail-early"],
      "evidence": "curl --fail http://127.0.0.1/ exits 2 on Curl.",
      "suggestion": "Parse --fail in Curl.Console.",
      "touches": ["Curl.Console"]
    }
  ],
  "notes": ""
}
```
