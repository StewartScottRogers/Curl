# Public Suffix List snapshot

The Public Suffix List (`public_suffix_list.dat`) is maintained by the Public Suffix List
project at https://publicsuffix.org/ and is used under the Mozilla Public License 2.0
(https://mozilla.org/MPL/2.0/). Curl embeds it unmodified, apart from a dated comment line
added first; its source form is this file and
https://publicsuffix.org/list/public_suffix_list.dat.

`public_suffix_list.dat` is embedded in `Curl.Cookies.UnitLibrary` with the manifest name
`Curl.Cookies.PublicSuffixList.public_suffix_list.dat`. Refresh it by running
`Update-PublicSuffixList.ps1` at the repository root and committing the result; the build
never downloads it. See ADR-0049.
