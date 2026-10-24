# Differential testing

The differential tests in `Curl.Conformance.Differential.IntegrationTests` run the same
case through the reference curl 8.22.0 (the real tool or its libcurl) and through Curl, and
write a `### DIFF` report of every difference (ADR-0782, ADR-0790, ADR-0791, ADR-0792). They
are Integration tests: they start processes and loopback servers. A test whose reference
variable is unset ends Inconclusive instead of running.

## The four variables

| Variable | Names | Read by |
| --- | --- | --- |
| `CURL_DIFFERENTIAL_REFERENCE` | The reference `curl.exe` (curl 8.22.0, Schannel build). | The command-line tests (API, HTTP, TLS, host). |
| `CURL_DIFFERENTIAL_REFERENCE_LIBRARY` | The reference `libcurl-4.dll`. | The libcurl easy, formadd, mime, multi, printf and WebSocket tests. |
| `CURL_DIFFERENTIAL_REFERENCE_LIBCURL` | The same library, under the name the libcurl scenario test reads. | `LibcurlScenarioDifferentialTests`. |
| `CURL_DIFFERENTIAL_PKI` | The directory of generated certificates (`New-DifferentialPki.ps1`). | The TLS test. |

Optional: `CURL_DIFFERENTIAL_REPORT` names a file for the report, and `CURL_DIFFERENTIAL_OURS`
names the Curl under test (default: the `Curl.Console` build).

## Setting them: `Set-DifferentialEnvironment.ps1`

Dot-source the script at the repository root so the variables stay set in your session:

```powershell
. .\Set-DifferentialEnvironment.ps1
```

It looks for a `curl.exe` whose `curl -V` reports `curl 8.22.0` and `Schannel`: first every
`curl.exe` on `PATH`, then the `ucrt64\bin` and `mingw64\bin` folders of the MSYS2 (or Git for
Windows) root found from where `bash.exe` or `pacman.exe` lives. It takes `libcurl-4.dll` from
the same folder, and runs `New-DifferentialPki.ps1` into `%LOCALAPPDATA%\Curl\differential-pki`
when that folder holds no `ca.pem`. It prints the four values, asks nothing and downloads nothing.

Parameters: `-ReferenceCurl <path>` and `-ReferenceLibrary <path>` skip the search, and
`-Pki <existing directory>` uses your own certificates. When no reference is found, or a path
you gave does not exist, it stops with an error naming what it looked for and how to use
`-ReferenceCurl`.

## Running the tests

Fast tests, which skip the differential tests (they are Integration):

```powershell
dotnet test --filter "TestCategory!=Integration"
```

The differential tests, after the script:

```powershell
dotnet test Curl.Conformance.Differential.IntegrationTests --filter "TestCategory=Integration"
```
