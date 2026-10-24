# ADR-0719: test1707.pl's `curl.txt` is real curl 8.22.0's `curl -M` text, vendored as test data

- Status: Accepted
- Date: 2026-10-21
- Decided by Claude under Stewart's delegation (BL-2866)

## Context

Upstream cases 1707, 1708 and 1710 run `test1707.pl %CURL <option> ../docs/cmdline-opts/curl.txt`,
which compares `curl -h <option>` with the option's section of `curl.txt`. `curl.txt` is not in the
release tarball: upstream's `managen` generates it from `docs/cmdline-opts/*.md` at build time, and
`src/mkhelp.pl` embeds it as the manual `curl -M` prints (BL-2864). Curl has no `managen` yet
(BL-2881), and the harness never runs Perl.

## Decision

- `Curl.Conformance.UnitTests/CurlTestCases/Original/docs/cmdline-opts/curl.txt` is the output of real
  curl 8.22.0's `curl -M` (the Windows Schannel build in `C:\Program Files\Git\ucrt64\bin`, line feeds only), measured
  on 2026-10-21. It is the text `managen` generated for that release, as embedded in the binary.
- `UpstreamTest1707Script` interprets test1707.pl in C# and reads that file from `docs` under the
  folder standing in for upstream's source tree (the conformance tests' `CurlTestCases/Original`).
- When BL-2881 builds `managen`, the file can be generated from the vendored `.md` files instead;
  until then `Update-UpstreamTestData.ps1` does not refresh it, since the tarball does not hold it.

## Why

It is upstream's own text for the targeted version, not Curl's (`Curl.Cli.UnitLibrary/CurlManual.txt`
happens to hold the same bytes today, but the check must not compare Curl with itself). Building
`managen` first would hold three passing cases behind a large, separately filed task.
