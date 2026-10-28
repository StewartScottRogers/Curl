#!/usr/bin/env bash
# =============================================================================
#  Setup-UnixFactoryHost.sh -- prepare a remote Linux (or macOS) machine to run
#                              the dark factory
# =============================================================================
#
#  Run it once, from a checkout of the repository or from a copy of this file:
#
#      bash Setup-UnixFactoryHost.sh
#
#  It installs or verifies, each a no-op when already satisfied:
#
#      git, Node.js and npm, Claude Code, herdr   (the same approach as hrdrClaudeNative.sh)
#      the repository clone                       ($HOME/Curl, or the checkout this file is in)
#      the .NET SDK that global.json pins         (dotnet-install.sh, into $HOME/.dotnet)
#      PowerShell 7 (pwsh)                        (dotnet tool, into $HOME/.dotnet/tools)
#      the GitHub CLI (gh)
#      a `powershell` shim on PATH pointing at pwsh
#      the audio-off file in the factory's data folder (nobody listens on a remote host)
#      git's identity                             (from GIT_USER_NAME and GIT_USER_EMAIL when unset)
#
#  Then it prints a checklist marking each of those and the three logins that cannot be
#  scripted (Claude Code, gh, git push credentials) present or missing, with the command
#  that fixes each missing one. It exits non-zero while anything is missing, and says so
#  when a second run changed nothing. After it: ./RunDarkFactory.sh -NewTab -Lanes Auto -Continuous
#
#  Written for Ubuntu and Debian first; macOS (brew) and other package managers where cheap.
# =============================================================================

set -u

SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_URL="https://github.com/StewartScottRogers/Curl.git"
DEFAULT_REPO_DIR="$HOME/Curl"
REPO_DIR="$DEFAULT_REPO_DIR"
[ -e "$SELF_DIR/.git" ] && REPO_DIR="$SELF_DIR"

if [ "$(uname -s)" = "Darwin" ]; then
    DATA_DIR="$HOME/Library/Application Support/Curl"
else
    DATA_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/Curl"
fi

CHANGES=0
changed() { CHANGES=$((CHANGES + 1)); }
say() { echo "[setup] $*"; }

SUDO=""
[ "$(id -u)" -ne 0 ] && command -v sudo >/dev/null 2>&1 && SUDO="sudo"
PKG=""
for candidate in brew apt-get dnf yum pacman zypper apk; do
    if command -v "$candidate" >/dev/null 2>&1; then PKG="$candidate"; break; fi
done

# pkg_install <brew/apt name> <dnf/yum/pacman/zypper/apk name>
pkg_install() {
    case "$PKG" in
        brew)    brew install "$1" ;;
        apt-get) $SUDO apt-get update -y && $SUDO env DEBIAN_FRONTEND=noninteractive apt-get install -y "$1" ;;
        dnf)     $SUDO dnf install -y "$2" ;;
        yum)     $SUDO yum install -y "$2" ;;
        pacman)  $SUDO pacman -Sy --noconfirm "$2" ;;
        zypper)  $SUDO zypper --non-interactive install "$2" ;;
        apk)     $SUDO apk add "$2" ;;
        *)       return 1 ;;
    esac
}

fetch_to_stdout() {
    if command -v curl >/dev/null 2>&1; then curl -fsSL "$1"
    elif command -v wget >/dev/null 2>&1; then wget -qO- "$1"
    else return 1
    fi
}

# Put every directory this script installs into on THIS session's PATH, and once on
# $HOME/.profile so later shells (and the herdr server they start) find the tools too.
export DOTNET_ROOT="$HOME/.dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$HOME/.local/bin:$HOME/.herdr/bin:$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
PROFILE_MARK="# added by Setup-UnixFactoryHost.sh"
if ! grep -qF "$PROFILE_MARK" "$HOME/.profile" 2>/dev/null; then
    {
        echo "$PROFILE_MARK"
        echo 'export DOTNET_ROOT="$HOME/.dotnet"'
        echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
        echo 'export PATH="$HOME/.local/bin:$HOME/.herdr/bin:$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"'
    } >> "$HOME/.profile"
    changed
fi

# ---- git ---------------------------------------------------------------------
if ! command -v git >/dev/null 2>&1; then
    say "installing git ..."
    pkg_install git git && changed
fi

# ---- curl (the installers below need it or wget) ---------------------------------
if ! command -v curl >/dev/null 2>&1 && ! command -v wget >/dev/null 2>&1; then
    say "installing curl ..."
    pkg_install curl curl; changed
fi

# ---- libraries the .NET SDK and its installer need (a minimal container has neither) --------
# dotnet stops with "Couldn't find a valid ICU package" without libicu; the installer needs tar.
if [ "$(uname -s)" != "Darwin" ] && [ -n "$PKG" ]; then
    if ! command -v tar >/dev/null 2>&1; then say "installing tar ..."; pkg_install tar tar; changed; fi
    icu_present=1
    case "$PKG" in
        apt-get) dpkg -l 'libicu[0-9]*' 2>/dev/null | grep -q '^ii' && icu_present=0 ;;
        *)       ldconfig -p 2>/dev/null | grep -q 'libicuuc' && icu_present=0 ;;
    esac
    if [ "$icu_present" -ne 0 ]; then
        say "installing ICU (libicu) for .NET ..."
        case "$PKG" in
            apt-get) pkg_install libicu-dev libicu ;;
            apk)     $SUDO apk add icu-libs ;;
            pacman)  $SUDO pacman -Sy --noconfirm icu ;;
            *)       pkg_install libicu libicu ;;
        esac && changed
    fi
fi

# ---- Node.js and npm ---------------------------------------------------------
if ! command -v npm >/dev/null 2>&1 || ! command -v node >/dev/null 2>&1; then
    say "installing Node.js and npm ..."
    case "$PKG" in
        brew)    brew install node ;;
        apt-get) pkg_install nodejs nodejs && $SUDO env DEBIAN_FRONTEND=noninteractive apt-get install -y npm ;;
        pacman)  $SUDO pacman -Sy --noconfirm nodejs npm ;;
        apk)     $SUDO apk add nodejs npm ;;
        *)       pkg_install nodejs nodejs && pkg_install npm npm ;;
    esac && changed
fi

# ---- Claude Code -------------------------------------------------------------
# npm's global prefix is often root-owned; install into ~/.local then.
if ! command -v claude >/dev/null 2>&1 && command -v npm >/dev/null 2>&1; then
    say "installing Claude Code ..."
    NPM_PREFIX="$(npm config get prefix 2>/dev/null)"
    NPM_PREFIX_ARGS=()
    if [ -n "$NPM_PREFIX" ] && [ ! -w "$NPM_PREFIX/lib" ] && [ ! -w "$NPM_PREFIX" ]; then
        NPM_PREFIX_ARGS=(--prefix "$HOME/.local")
    fi
    npm install -g ${NPM_PREFIX_ARGS[@]+"${NPM_PREFIX_ARGS[@]}"} @anthropic-ai/claude-code && changed
fi

# ---- herdr -------------------------------------------------------------------
if ! command -v herdr >/dev/null 2>&1; then
    say "installing herdr ..."
    fetch_to_stdout https://herdr.dev/install.sh | sh
    changed
fi
if command -v herdr >/dev/null 2>&1; then
    # Only the claude hook matters to the factory, and herdr can install it once claude has run.
    if command -v claude >/dev/null 2>&1 \
        && ! herdr integration status 2>/dev/null | grep -Eiq "claude: *(current|installed)"; then
        say "installing herdr claude integration hook ..."
        herdr integration install claude >/dev/null 2>&1 && changed
    fi
fi

# ---- the repository ----------------------------------------------------------
if [ ! -e "$REPO_DIR/.git" ] && command -v git >/dev/null 2>&1; then
    say "cloning $REPO_URL into $REPO_DIR ..."
    git clone "$REPO_URL" "$REPO_DIR"; changed
fi

# ---- .NET SDK pinned by global.json -----------------------------------------
# global.json pins "10.0.x" with rollForward latestFeature, so the 10.0 channel satisfies it.
DOTNET_CHANNEL="10.0"
if [ -f "$REPO_DIR/global.json" ]; then
    pinned="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([0-9]*\.[0-9]*\)\..*/\1/p' "$REPO_DIR/global.json" | head -1)"
    [ -n "$pinned" ] && DOTNET_CHANNEL="$pinned"
fi
dotnet_has_sdk() { command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q "^${DOTNET_CHANNEL}\."; }
if ! dotnet_has_sdk; then
    say "installing the .NET SDK $DOTNET_CHANNEL into $DOTNET_ROOT ..."
    fetch_to_stdout https://dot.net/v1/dotnet-install.sh > "${TMPDIR:-/tmp}/dotnet-install.$$.sh" \
        && bash "${TMPDIR:-/tmp}/dotnet-install.$$.sh" --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_ROOT"
    rm -f "${TMPDIR:-/tmp}/dotnet-install.$$.sh"
    changed
fi

# ---- PowerShell 7 ------------------------------------------------------------
# The same on every distribution: the PowerShell global tool, which needs the SDK above.
if ! command -v pwsh >/dev/null 2>&1 && command -v dotnet >/dev/null 2>&1; then
    say "installing PowerShell 7 as a dotnet tool ..."
    dotnet tool install --global PowerShell && changed
fi

# ---- the powershell shim -----------------------------------------------------
# Prompt text and skills say `powershell -NoProfile ...`; on this host that runs pwsh.
if ! command -v powershell >/dev/null 2>&1 && command -v pwsh >/dev/null 2>&1; then
    say "linking powershell to pwsh ..."
    mkdir -p "$HOME/.local/bin"
    ln -sf "$(command -v pwsh)" "$HOME/.local/bin/powershell"
    changed
fi

# ---- the GitHub CLI ----------------------------------------------------------
if ! command -v gh >/dev/null 2>&1; then
    say "installing the GitHub CLI ..."
    { pkg_install gh gh || pkg_install github-cli github-cli; } && changed
fi

# ---- audio off ---------------------------------------------------------------
if [ ! -e "$DATA_DIR/audio-off" ]; then
    say "creating the audio-off file ..."
    mkdir -p "$DATA_DIR" && : > "$DATA_DIR/audio-off"
    changed
fi

# ---- git identity ------------------------------------------------------------
if command -v git >/dev/null 2>&1; then
    if [ -z "$(git config --global user.name 2>/dev/null)" ] && [ -n "${GIT_USER_NAME:-}" ]; then
        git config --global user.name "$GIT_USER_NAME"; changed
    fi
    if [ -z "$(git config --global user.email 2>/dev/null)" ] && [ -n "${GIT_USER_EMAIL:-}" ]; then
        git config --global user.email "$GIT_USER_EMAIL"; changed
    fi
fi

# ---- git push credentials: let gh supply them once gh is signed in ----------------
if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1 \
    && [ -z "$(git config --global credential.helper 2>/dev/null)" ] \
    && [ -z "$(git config --global credential.https://github.com.helper 2>/dev/null)" ]; then
    gh auth setup-git && changed
fi

# =============================================================================
#  Checklist
# =============================================================================
MISSING=0
check() { # check <item> <ok: 0 means present> <what to run when missing>
    if [ "$2" -eq 0 ]; then
        printf '  [x] %s\n' "$1"
    else
        printf '  [ ] %s  -> %s\n' "$1" "$3"
        MISSING=$((MISSING + 1))
    fi
}
has() { command -v "$1" >/dev/null 2>&1; echo $?; }

echo
echo "Dark factory host checklist"
check "git"                      "$(has git)"    "install git with your package manager"
check "Node.js and npm"          "$(( $(has node) + $(has npm) ))" "install Node.js with your package manager"
check "Claude Code (claude)"     "$(has claude)" "npm install -g @anthropic-ai/claude-code"
check "herdr"                    "$(has herdr)"  "curl -fsSL https://herdr.dev/install.sh | sh"
check "repository at $REPO_DIR"  "$([ -e "$REPO_DIR/.git" ]; echo $?)" "git clone $REPO_URL $REPO_DIR"
check ".NET SDK $DOTNET_CHANNEL" "$(dotnet_has_sdk; echo $?)" "bash Setup-UnixFactoryHost.sh (needs network)"
check "PowerShell 7 (pwsh)"      "$(has pwsh)"   "dotnet tool install --global PowerShell"
check "powershell shim"          "$(has powershell)" "ln -s \"\$(command -v pwsh)\" ~/.local/bin/powershell"
check "GitHub CLI (gh)"          "$(has gh)"     "install gh: https://github.com/cli/cli/blob/trunk/docs/install_linux.md"
check "audio-off file"           "$([ -e "$DATA_DIR/audio-off" ]; echo $?)" "touch \"$DATA_DIR/audio-off\""
check "git identity"             "$([ -n "$(git config --global user.name 2>/dev/null)" ] && [ -n "$(git config --global user.email 2>/dev/null)" ]; echo $?)" \
    "git config --global user.name 'Your Name'; git config --global user.email you@example.com"

claude_signed_in=1
if [ -n "${ANTHROPIC_API_KEY:-}" ] || [ -s "$HOME/.claude/.credentials.json" ] \
    || grep -q '"oauthAccount"' "$HOME/.claude.json" 2>/dev/null; then claude_signed_in=0; fi
gh_signed_in=1
command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1 && gh_signed_in=0
push_ready=1
if [ -n "$(git config --global credential.helper 2>/dev/null)" ] \
    || [ -n "$(git config --global credential.https://github.com.helper 2>/dev/null)" ] \
    || [ -s "$HOME/.git-credentials" ]; then push_ready=0; fi

echo "Logins (cannot be scripted)"
check "Claude Code signed in"    "$claude_signed_in" "run: claude   (then sign in)"
check "gh signed in"             "$gh_signed_in"     "run: gh auth login"
check "git push credentials"     "$push_ready"       "run: gh auth login, then gh auth setup-git"

echo
if [ "$CHANGES" -eq 0 ]; then
    say "this run changed nothing."
else
    say "$CHANGES change(s) made."
fi
if [ "$MISSING" -gt 0 ]; then
    say "$MISSING item(s) missing. Fix them and run this script again."
    exit 1
fi
say "all present. Start a shift: cd \"$REPO_DIR\" && ./RunDarkFactory.sh -NewTab -Lanes Auto -Continuous"
