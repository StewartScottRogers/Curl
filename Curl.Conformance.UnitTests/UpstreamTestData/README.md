# Upstream test data

Every `test*` file from `tests/data` in curl at release tag **`curl-8_22_0`** (2072 files),
stored here as `test*.curltest` byte for byte, with curl's `COPYING` notice from the same
tag beside them. Each is one curl test case in `tests/data`'s own format (server reply, command
line, expected results) for whatever protocols the case uses, hence `.curltest` (BL-2157). They are the
upstream test cases the conformance harness runs (ADR-0013, decision 3: vendored and
pinned; tests never download anything).

- Source: https://github.com/curl/curl/tree/curl-8_22_0/tests/data
- Licence: the curl licence, in `COPYING` here (https://curl.se/docs/copyright.html).
- `.gitattributes` marks the `test*.curltest` files `-text`, so git keeps their line endings exactly.
- `Curl.Conformance.UnitTests.csproj` copies the `test*.curltest` files and `COPYING` to the test
  output directory; `UpstreamTestDataTests` asserts the count.

Do not edit these files by hand.

## certs

`certs/` holds every file of `tests/certs` in curl 8.22.0 (tag `curl-8_22_0`, the same files as
the release tarball https://curl.se/download/curl-8.22.0.tar.xz) except its build files
(`Makefile.*`, `CMakeLists.txt`): `genserv.pl`, `test-ca.cnf` and the `test-*.prm` certificate
parameters, byte for byte (BL-1922, BL-2153). 8.22.0 dropped the `srp-verifier-*` files. The tarball ships no certificate
files - upstream generates `test-ca.crt`, `test-localhost.pem` and the rest at build time with
`genserv.pl` and OpenSSL - so `%CERTDIR/certs/<name>.crt` does not exist here yet; generating
them is BL-1923. `UpstreamConformanceTests` passes this folder as `%CERTDIR`, and the csproj
copies `certs/` to the test output beside the cases. `.gitattributes` marks them `-text`.
`Update-UpstreamTestData.ps1` refreshes this folder with the cases.

## data

`data/` holds the two files of `tests/data` that httpsig cases 5001 to 5025 name as
`%SRCDIR/data/...` (BL-2183): `data-httpsig-ed25519.key` and `data-httpsig-hmac-sha256.key`, byte
for byte. `UpstreamConformanceTests` passes this folder as `%SRCDIR`; any other `%SRCDIR/data/...` file is
not vendored, so a case that names one fails with curl's read error. The csproj copies `data/` to
the test output; `.gitattributes` marks the files `-text`.

## Refresh to another tag

```
powershell -NoProfile -ExecutionPolicy Bypass -File Curl.Conformance.UnitTests\UpstreamTestData\Update-UpstreamTestData.ps1 -Tag curl-8_22_0
```

The script downloads the tag's source archive from GitHub, replaces every `test*.curltest` file in
this folder, every file in `certs/` (from `tests/certs`, build files and dot files left out) and
`COPYING` wholesale, and prints the new count. After a refresh, update the
tag and count in this README and `VendoredTestFileCount` in `UpstreamTestDataTests.cs`.
Run with the pinned tag, it reproduces the folder with an empty `git status`.
