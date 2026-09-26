#!/bin/sh
# Installs the C# port of curl on Linux or macOS from its GitHub release.
#
#   curl -fsSL https://raw.githubusercontent.com/StewartScottRogers/Curl/master/install.sh | sh
#
# Environment:
#   CURL_VERSION      release tag to install, e.g. v0.1.0 (default: the newest release,
#                     pre-releases included)
#   CURL_INSTALL_DIR  where the binary goes (default: $HOME/.curl-dotnet/bin)
#
# The binary is named `curl`. It is installed into a directory of its own so it never
# shadows the system curl by accident; put that directory first on PATH to make it the
# drop-in replacement. See DOWNLOAD.md.
set -eu

repo="StewartScottRogers/Curl"
version="${CURL_VERSION:-latest}"
dir="${CURL_INSTALL_DIR:-$HOME/.curl-dotnet/bin}"

case "$(uname -s)" in
  Linux)  os=linux ;;
  Darwin) os=osx ;;
  *) echo "install.sh: unsupported operating system $(uname -s); see DOWNLOAD.md" >&2; exit 1 ;;
esac
case "$(uname -m)" in
  x86_64|amd64)  arch=x64 ;;
  aarch64|arm64) arch=arm64 ;;
  *) echo "install.sh: unsupported processor $(uname -m); see DOWNLOAD.md" >&2; exit 1 ;;
esac
package="curl-$os-$arch.tar.gz"

fetch() {
  if command -v curl >/dev/null 2>&1; then curl -fsSL -o "$2" "$1"
  elif command -v wget >/dev/null 2>&1; then wget -qO "$2" "$1"
  else echo "install.sh: needs curl or wget to download" >&2; exit 1
  fi
}

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# releases/latest skips pre-releases, so ask the API for the newest release of any kind.
if [ "$version" = latest ]; then
  fetch "https://api.github.com/repos/$repo/releases?per_page=1" "$tmp/releases.json"
  version="$(sed -n 's/.*"tag_name": *"\([^"]*\)".*/\1/p' "$tmp/releases.json" | head -n 1)"
  if [ -z "$version" ]; then
    echo "install.sh: no release found at https://github.com/$repo/releases" >&2
    exit 1
  fi
fi
base="https://github.com/$repo/releases/download/$version"

echo "Downloading $package ($version)"
fetch "$base/$package" "$tmp/$package"
fetch "$base/SHA256SUMS" "$tmp/SHA256SUMS"

expected="$(grep " $package\$" "$tmp/SHA256SUMS" | cut -d' ' -f1)"
if command -v sha256sum >/dev/null 2>&1; then
  actual="$(sha256sum "$tmp/$package" | cut -d' ' -f1)"
else
  actual="$(shasum -a 256 "$tmp/$package" | cut -d' ' -f1)"
fi
if [ -z "$expected" ] || [ "$expected" != "$actual" ]; then
  echo "install.sh: checksum mismatch for $package" >&2
  exit 1
fi

tar -xzf "$tmp/$package" -C "$tmp" curl
mkdir -p "$dir"
mv "$tmp/curl" "$dir/curl"
chmod 755 "$dir/curl"

echo "Installed $dir/curl"
case ":$PATH:" in
  *":$dir:"*) ;;
  *) echo "To use it in place of the system curl, add this to your shell profile:"
     echo "  export PATH=\"$dir:\$PATH\"" ;;
esac
