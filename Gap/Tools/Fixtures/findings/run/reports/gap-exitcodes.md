```json
{
  "analyst": "gap-exitcodes",
  "area": "exitcodes",
  "run": "2026-10-09_1430",
  "groups": [
    {
      "title": "Exit code 7 text differs",
      "severity": "Medium",
      "introducedIn": null,
      "items": ["exitcodes:7"],
      "evidence": "curl http://127.0.0.1:1/ prints different text.",
      "suggestion": "Fix the text in Curl.Core.UnitLibrary.",
      "touches": ["Curl.Core.UnitLibrary"]
    }
  ],
  "notes": ""
}
```
