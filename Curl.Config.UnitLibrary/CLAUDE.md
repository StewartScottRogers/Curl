# Curl.Config.UnitLibrary

`curl-config`'s answers (ADR-0712), namespace `Curl.Config`:

- `CurlConfigCommand` - each option as curl 8.22.0's `curl-config.in` answers it, with the values
  ADR-0712 decides; the usage text word for word; an unknown option prints `unknown option: <opt>`
  and the usage on standard output and exits 1, as the script does.
- `CurlConfigVersionCheck` - `--checkfor`'s field-by-field comparison, with `cut` and `sh test` semantics.
- `CurlConfigBuild` - the build the answers describe, read from `Libcurl`'s `curl_version_info`
  (`CurlVersion.Info`), so `curl-config` and `curl --version` cannot drift apart.
- `CurlConfigProgram` - the executable's run: initialise libcurl, answer, clean up.

`Curl.Conformance.UnitLibrary` runs these answers in place of `../curl-config` when it emulates
upstream's `test1013.pl` and `test1022.pl`. Held to the four quality gates in the root `CLAUDE.md`.
