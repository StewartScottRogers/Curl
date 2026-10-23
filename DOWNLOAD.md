# Download and install

Curl ships as one native executable per platform, named `curl` (`curl.exe` on Windows),
with no .NET runtime to install. Every package is built by the
[Release workflow](.github/workflows/release.yml) and attached to a
[GitHub release](https://github.com/StewartScottRogers/Curl/releases) with a
`SHA256SUMS` file.

> **Status:** the port is still in progress. The binary parses curl's options, runs
> transfers and returns curl's exit codes, but it does not yet match upstream curl
> everywhere (see the [gap dashboard](https://stewartscottrogers.github.io/Curl/gaps/)).
> Keep your system curl on PATH ahead of it until you have checked your scripts against it.

## Supported platforms

Current version: **[the latest build](https://github.com/StewartScottRogers/Curl/releases/tag/preview)**, a pre-release rebuilt from `master` on every merge, so these links always give the newest Curl. The newest numbered pre-release is [v0.2.0-preview.1](https://github.com/StewartScottRogers/Curl/releases/tag/v0.2.0-preview.1).

| Platform | Processor | Package |
| --- | --- | --- |
| Windows | x64 | [curl-win-x64.zip](https://github.com/StewartScottRogers/Curl/releases/download/preview/curl-win-x64.zip) |
| Windows | Arm64 | [curl-win-arm64.zip](https://github.com/StewartScottRogers/Curl/releases/download/preview/curl-win-arm64.zip) |
| Linux | x64 | [curl-linux-x64.tar.gz](https://github.com/StewartScottRogers/Curl/releases/download/preview/curl-linux-x64.tar.gz) |
| Linux | Arm64 | [curl-linux-arm64.tar.gz](https://github.com/StewartScottRogers/Curl/releases/download/preview/curl-linux-arm64.tar.gz) |
| macOS | Apple silicon | [curl-osx-arm64.tar.gz](https://github.com/StewartScottRogers/Curl/releases/download/preview/curl-osx-arm64.tar.gz) |
| macOS | Intel | [curl-osx-x64.tar.gz](https://github.com/StewartScottRogers/Curl/releases/download/preview/curl-osx-x64.tar.gz) |

Checksums: [SHA256SUMS](https://github.com/StewartScottRogers/Curl/releases/download/preview/SHA256SUMS).
Older versions are on the [releases page](https://github.com/StewartScottRogers/Curl/releases).
Linux packages are built on Ubuntu 22.04 and need glibc 2.35 or newer (Ubuntu 22.04, Debian 12 and later). Alpine and other musl-based distributions are not supported yet. Windows and macOS versions follow the [.NET 10 supported OS list](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

## Curl.Mcp (MCP server for AI agents)

Curl.Mcp runs Curl for AI agents over the Model Context Protocol (standard input and
output). Its seven NuGet packages are attached to each release, at the release's version.
**They are not on nuget.org yet**; that waits until the port is complete, so install from
a folder. See [Curl.Mcp/README.md](Curl.Mcp/README.md) for the tools it offers.

| Package | What it is |
| --- | --- |
| [Curl.Mcp](https://github.com/StewartScottRogers/Curl/releases/tag/preview) | The tool package (`curl-mcp`) with `.mcp/server.json` |
| [Curl.Mcp.win-x64](https://github.com/StewartScottRogers/Curl/releases/tag/preview) | Windows x64 |
| [Curl.Mcp.win-arm64](https://github.com/StewartScottRogers/Curl/releases/tag/preview) | Windows Arm64 |
| [Curl.Mcp.linux-x64](https://github.com/StewartScottRogers/Curl/releases/tag/preview) | Linux x64 |
| [Curl.Mcp.linux-arm64](https://github.com/StewartScottRogers/Curl/releases/tag/preview) | Linux Arm64 |
| [Curl.Mcp.osx-x64](https://github.com/StewartScottRogers/Curl/releases/tag/preview) | macOS Intel |
| [Curl.Mcp.osx-arm64](https://github.com/StewartScottRogers/Curl/releases/tag/preview) | macOS Apple silicon |

Each package's file name carries its build's version, which changes with every build, so these
link to the latest build's release page, where all seven are listed.

To run one without nuget.org, download all seven into one folder and install from it
(the tool package picks the matching `Curl.Mcp.<rid>` package from the same folder):

```sh
dotnet tool install --global --add-source ./folder-with-the-packages Curl.Mcp --prerelease
CURL_MCP_ROOT="$HOME/curl-work" curl-mcp
```

`CURL_MCP_ROOT` is required: it is the directory every file curl reads or writes must lie
inside. `CURL_MCP_MAX_RESPONSE_BYTES` caps the output a tool returns (1048576 when unset).
Verify each download against the release's `SHA256SUMS`, which covers the `.nupkg` files too.

## Install with one command

The install scripts pick the right package for the machine, check its SHA-256, and put
the binary in a directory of its own so it never replaces the system curl by accident.

**Linux and macOS** - installs to `~/.curl-dotnet/bin`:

```sh
curl -fsSL https://raw.githubusercontent.com/StewartScottRogers/Curl/master/install.sh | sh
```

Pick a version or directory with `CURL_VERSION=v0.1.0` or `CURL_INSTALL_DIR=/usr/local/bin`
in front of `sh`.

**Windows (PowerShell)** - installs to `%LOCALAPPDATA%\Programs\curl-dotnet\bin`:

```powershell
irm https://raw.githubusercontent.com/StewartScottRogers/Curl/master/install.ps1 | iex
```

To pass options, download it first: `.\install.ps1 -Version v0.1.0 -AddToPath`.

## Install by hand

### Windows

1. Download the `.zip` for your processor from the table above.
2. Right-click it, **Properties**, tick **Unblock**, **OK**.
3. Extract `curl.exe` to a folder of your choice, e.g. `C:\Tools\curl-dotnet`.
4. Run it by full path: `C:\Tools\curl-dotnet\curl.exe --version`.

### Linux

```sh
tar -xzf curl-linux-x64.tar.gz curl
sudo install -m 755 curl /usr/local/bin/curl-dotnet   # or keep the name curl in a directory of its own
```

### macOS

```sh
tar -xzf curl-osx-arm64.tar.gz curl
xattr -d com.apple.quarantine curl 2>/dev/null || true   # the binary is not notarised
mkdir -p ~/.curl-dotnet/bin && mv curl ~/.curl-dotnet/bin/
```

## Make it the drop-in replacement

The point of the port is that scripts calling `curl` cannot tell the difference, which
means putting this binary ahead of the system one on PATH.

- **Linux and macOS:** add `export PATH="$HOME/.curl-dotnet/bin:$PATH"` to `~/.bashrc`,
  `~/.zshrc` or your shell's profile.
- **Windows:** `C:\Windows\System32\curl.exe` is on the machine PATH, which Windows
  searches before the user PATH. Move the install directory above `%SystemRoot%\System32`
  in the *system* PATH (needs an administrator), or call the port by its full path.

Check which one runs with `command -v curl` (Linux, macOS) or `where.exe curl` (Windows).

## Uninstall

Delete the binary: `rm ~/.curl-dotnet/bin/curl`, or
`Remove-Item "$env:LOCALAPPDATA\Programs\curl-dotnet" -Recurse` on Windows, and remove
the PATH entry if you added one.

## Build it yourself

With the .NET 10 SDK and the platform's native toolchain (Visual Studio's C++ workload
on Windows, `clang` and `zlib1g-dev` on Linux, Xcode command-line tools on macOS):

```sh
dotnet publish Curl.Console -c Release -r linux-x64 -o publish
```

Replace `linux-x64` with any identifier from the table. Native AOT only builds for the
operating system it runs on.

## For AI agents: resources.json and ai-help.md

Every build of the rolling `preview` release (and every numbered release) also carries two
files that an agent can start from:

- `resources.json` at `https://github.com/StewartScottRogers/Curl/releases/download/preview/resources.json`
  lists every other file in the release. It is made from the files actually attached, and the
  release run fails if it disagrees with them.
- `ai-help.md` is the output of `curl --ai-help all` from that same build.

`resources.json` is UTF-8 JSON with `schema` (1), `name`, `version`, `commit`, `release` (the
release page URL) and `resources`, one entry per file with `name`, `kind`, `platform`, `file`,
`url`, `sha256`, `size` (bytes), `use` (one line on how to run or reference it) and `docs`.
`kind` is `cli` (a curl archive), `mcp-server` (a Curl.Mcp package), `dotnet-library` (Curl.Api
or Libcurl), `checksums` (`SHA256SUMS`) or `ai-help`; `platform` is a runtime identifier from
the table above or `any`. The generator, `.github/release/New-ResourcesJson.ps1`, documents the
same fields.
