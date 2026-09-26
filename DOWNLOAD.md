# Download and install

Curl ships as one native executable per platform, named `curl` (`curl.exe` on Windows),
with no .NET runtime to install. Every package is built by the
[Release workflow](.github/workflows/release.yml) and attached to a
[GitHub release](https://github.com/StewartScottRogers/Curl/releases) with a
`SHA256SUMS` file.

> **Status:** the port is in Phase 1. Until the option parser and transfer engine land,
> the binary starts, prints `curl: not implemented yet` and exits with code 2
> (`CURLE_FAILED_INIT`). Keep your system curl on PATH ahead of it for now.

## Supported platforms

| Platform | Processor | Package |
| --- | --- | --- |
| Windows | x64 | [curl-win-x64.zip](https://github.com/StewartScottRogers/Curl/releases/latest/download/curl-win-x64.zip) |
| Windows | Arm64 | [curl-win-arm64.zip](https://github.com/StewartScottRogers/Curl/releases/latest/download/curl-win-arm64.zip) |
| Linux | x64 | [curl-linux-x64.tar.gz](https://github.com/StewartScottRogers/Curl/releases/latest/download/curl-linux-x64.tar.gz) |
| Linux | Arm64 | [curl-linux-arm64.tar.gz](https://github.com/StewartScottRogers/Curl/releases/latest/download/curl-linux-arm64.tar.gz) |
| macOS | Apple silicon | [curl-osx-arm64.tar.gz](https://github.com/StewartScottRogers/Curl/releases/latest/download/curl-osx-arm64.tar.gz) |
| macOS | Intel | [curl-osx-x64.tar.gz](https://github.com/StewartScottRogers/Curl/releases/latest/download/curl-osx-x64.tar.gz) |

Checksums: [SHA256SUMS](https://github.com/StewartScottRogers/Curl/releases/latest/download/SHA256SUMS).
Older versions are on the [releases page](https://github.com/StewartScottRogers/Curl/releases).
Linux packages are built on Ubuntu 22.04 and need glibc 2.35 or newer (Ubuntu 22.04, Debian 12 and later). Alpine and other musl-based distributions are not supported yet. Windows and macOS versions follow the [.NET 10 supported OS list](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

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
