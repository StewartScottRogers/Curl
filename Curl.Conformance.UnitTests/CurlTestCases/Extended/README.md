# CurlTestCases/Extended

Our own conformance cases, beside `../Original/` (upstream curl's `tests/data`, vendored byte for byte).
`Update-UpstreamTestData.ps1` writes only into `Original/` and never touches this folder, so a refresh
from a curl release tag leaves these cases alone.

- **Naming.** `ext<number>.curltest` (for example `ext1.curltest`). The `ext` prefix means a number never
  collides with an upstream `test<number>.curltest`.
- **Format.** The same `tests/data` format upstream uses (`<testcase>`, `<client>`, `<reply>`, `<verify>`),
  so `UpstreamCaseRunner` runs them unchanged.
- **Run environment.** `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` runs every case here
  with `Original/`'s `certs/` as `%CERTDIR` and `Original/`'s `data/` as `%SRCDIR/data`.
- **Not upstream.** These cases are not on `PassingUpstreamCases.txt` and never change the upstream pass
  count, the ratchet or the gap office's behaviour score; each one must simply pass.
