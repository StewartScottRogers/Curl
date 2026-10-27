#!/usr/bin/env bash
# =============================================================================
#  hrdrClaudeNative.sh  --  one-file portable Claude-in-herdr dev bootstrap
#                           (Linux and macOS twin of hrdrClaudeNative.cmd)
# =============================================================================
#
#  WHAT THIS IS
#  ------------
#  A single, self-contained Bash script. Copy it to a bare Linux or macOS
#  machine, run it, and in one execution it stands up a working dev environment
#  for the Curl project -- no other files required.
#
#      bash hrdrClaudeNative.sh
#
#  It performs these steps, each a no-op if already satisfied:
#
#      [1/7] Ensure git            (installs via the system package manager)
#      [2/7] Ensure Node.js / npm  (installs via the system package manager)
#      [3/7] Ensure Claude Code    (installs/updates via npm)
#      [4/7] Ensure herdr          (installs/updates the terminal multiplexer)
#      [5/7] Clone / update repo   (into $HOME/Curl)
#      [6/7] Ensure herdr server   (stops an incompatible one)
#      [7/7] Launch Claude         (as a tracked pane anchored to the repo)
#
#  Claude shows up as a tracked "Claude" pane in herdr's sidebar; its live state
#  (idle / working / blocked) is reported by herdr's native Claude integration
#  hook. Step [4/7] guarantees both the "claude" and "copilot" integration hooks
#  are installed, running each install unless status confirms it already is.
#
#  WHERE IT RUNS THE REPO FROM
#  ---------------------------
#  * Launched from inside ANY git checkout -> uses that checkout as-is, whatever
#    repository it belongs to. No path in this file is hard-coded.
#  * Launched from anywhere else (e.g. Downloads) -> clones a fresh copy into
#    $HOME/Curl.
#
#  REQUIREMENTS ON A BARE MACHINE
#  ------------------------------
#  * One of brew, apt-get, dnf, yum, pacman, zypper or apk installs git and
#    Node.js when they are absent; the Linux ones run through sudo.
#  * curl or wget downloads the herdr installer.
#  * The repo is public, so cloning needs no credentials.
#  * Claude Code will ask you to sign in on first run -- that cannot be baked in.
#
#  CUSTOMISING THE MODEL
#  ---------------------
#      export CLAUDE_MODEL="claude-opus-5-5"     (e.g. in ~/.bashrc or ~/.zshrc)
#  (default below is "opus", a moving alias Claude Code resolves to the newest
#   Opus release at launch, so this script does not go stale when a better
#   model ships. Set CLAUDE_MODEL to pin an exact id instead.)
#
#  HOW IT DIFFERS FROM THE .cmd
#  ----------------------------
#  Windows opens the herdr server in a new console window. A Unix terminal has
#  no portable "new window", so when no server is running this script starts
#  the Claude pane from a background job and attaches THIS terminal to herdr in
#  the foreground; the background job waits for the server, creates the pane
#  and types the claude command into it. Its output goes to a log file so it
#  never draws over herdr's screen.
#
#  JSON from herdr's API is parsed with node, which step [2/7] guarantees.
# =============================================================================

set -u

# Anchor to this script's own folder so every step runs relative to where the
# script lives, not the caller's current directory.
SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SELF_DIR" || exit 1

die() {
    echo
    echo "ERROR: $*"
    echo
    # Keep a double-clicked terminal open long enough to read the message.
    if [ -t 0 ]; then read -r -p "Press Enter to close ..." _; fi
    exit 1
}

# -----------------------------------------------------------------------------
#  Configuration
# -----------------------------------------------------------------------------
export CLAUDE_MODEL="${CLAUDE_MODEL:-opus}"
REPO_URL="https://github.com/StewartScottRogers/Curl.git"
DEFAULT_REPO_DIR="$HOME/Curl"
# The herdr workspace Claude always opens in. Step 7 reuses the workspace with
# this label, or creates it when none exists yet.
export HERDR_WORKSPACE_LABEL="Curl"

# If the script's folder is any git checkout we use it in place; otherwise we
# fall back to the clone location and clone in step 5.
REPO_DIR="$DEFAULT_REPO_DIR"
[ -e "$SELF_DIR/.git" ] && REPO_DIR="$SELF_DIR"
export REPO_DIR

# The system package manager is our fallback installer for git and Node.js.
# An empty PKG means "not available", which is only fatal if a tool needs it.
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
        apt-get) $SUDO apt-get update -y && $SUDO apt-get install -y "$1" ;;
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


# -----------------------------------------------------------------------------
#  [1/7]  Ensure git  (needed to clone the repo)
# -----------------------------------------------------------------------------
echo "[1/7] Checking for git ..."
if ! command -v git >/dev/null 2>&1; then
    echo "      git not found - installing via ${PKG:-a package manager} ..."
    [ -n "$PKG" ] || die "no supported package manager found, so git cannot be auto-installed. Install git, then retry."
    pkg_install git git
    command -v git >/dev/null 2>&1 || die "git could not be installed or located. Install git, then retry."
fi


# -----------------------------------------------------------------------------
#  [2/7]  Ensure Node.js / npm  (needed to install Claude Code)
# -----------------------------------------------------------------------------
echo "[2/7] Checking for Node.js / npm ..."
if ! command -v npm >/dev/null 2>&1 || ! command -v node >/dev/null 2>&1; then
    echo "      Node.js not found - installing via ${PKG:-a package manager} ..."
    [ -n "$PKG" ] || die "no supported package manager found, so Node.js cannot be auto-installed. Install Node.js LTS from nodejs.org, then retry."
    case "$PKG" in
        brew)    brew install node ;;
        apt-get) pkg_install nodejs nodejs && $SUDO apt-get install -y npm ;;
        pacman)  $SUDO pacman -Sy --noconfirm nodejs npm ;;
        apk)     $SUDO apk add nodejs npm ;;
        *)       pkg_install nodejs nodejs && pkg_install npm npm ;;
    esac
    command -v npm >/dev/null 2>&1 || die "npm could not be installed or located. Install Node.js LTS from nodejs.org, then retry."
fi

# npm's global prefix is often root-owned on Linux (/usr). Rather than run npm
# under sudo, install global packages into ~/.local when the prefix is not
# writable, and put its bin directory on THIS session's PATH -- the herdr
# server started later inherits it, so `claude` resolves in its panes too.
NPM_PREFIX="$(npm config get prefix 2>/dev/null)"
NPM_PREFIX_ARGS=()
if [ -n "$NPM_PREFIX" ] && [ ! -w "$NPM_PREFIX/lib" ] && [ ! -w "$NPM_PREFIX" ]; then
    NPM_PREFIX="$HOME/.local"
    NPM_PREFIX_ARGS=(--prefix "$NPM_PREFIX")
fi
export PATH="$PATH:$NPM_PREFIX/bin"


# -----------------------------------------------------------------------------
#  [3/7]  Ensure Claude Code  (npm global package), then update it
# -----------------------------------------------------------------------------
#  Missing  -> install. Present -> best-effort update (an offline failure is
#  swallowed so the launch still proceeds on the version already installed).
echo "[3/7] Checking for Claude Code ..."
if command -v claude >/dev/null 2>&1; then
    echo "      updating Claude Code ..."
    claude update >/dev/null 2>&1 || true
else
    echo "      installing Claude Code via npm ..."
    # The ${...+...} form keeps macOS's Bash 3.2 happy with an empty array under set -u.
    npm install -g ${NPM_PREFIX_ARGS[@]+"${NPM_PREFIX_ARGS[@]}"} @anthropic-ai/claude-code \
        || die "npm could not install Claude Code (exit code $?). Check your network connection and npm's output above, then retry."
    command -v claude >/dev/null 2>&1 || die "Claude Code installed but 'claude' is not on PATH ($NPM_PREFIX/bin)."
fi


# -----------------------------------------------------------------------------
#  [4/7]  Ensure herdr, update it, and install its integration hooks
# -----------------------------------------------------------------------------
#  Locate herdr on PATH first, then at the usual per-user install locations. If
#  missing, run the official Unix installer, then probe again.
echo "[4/7] Checking for herdr ..."
find_herdr() {
    HERDR="$(command -v herdr 2>/dev/null || true)"
    for p in "$HOME/.local/bin/herdr" "$HOME/.herdr/bin/herdr"; do
        [ -z "$HERDR" ] && [ -x "$p" ] && HERDR="$p"
    done
}
find_herdr
if [ -z "$HERDR" ]; then
    echo "      herdr not found - installing ..."
    fetch_to_stdout https://herdr.dev/install.sh | sh
    export PATH="$PATH:$HOME/.local/bin:$HOME/.herdr/bin"
    find_herdr
fi
[ -n "$HERDR" ] || die "herdr could not be installed or located. Install it from herdr.dev, or add its bin directory to PATH, then retry."
export HERDR

# Keep herdr current (best-effort; ignore failures on an offline/pinned box).
echo "      updating herdr ..."
"$HERDR" update >/dev/null 2>&1 || true

# Guarantee both native integration hooks are installed. Skip an install ONLY
# when `integration status` explicitly reports that hook as present ("<name>:
# current (vN)", or "installed" in older builds); any other outcome runs the
# install, which herdr treats as idempotent. "<name>: not installed" does not
# match because "not" breaks the pattern.
ensure_integration() {
    if "$HERDR" integration status 2>/dev/null | grep -Eiq "$1: *(current|installed)"; then
        echo "      herdr $1 integration already installed"
    else
        echo "      installing herdr $1 integration hook ..."
        "$HERDR" integration install "$1"
    fi
}
ensure_integration claude
ensure_integration copilot


# -----------------------------------------------------------------------------
#  [5/7]  Clone or update the repo  (skipped when using an in-place checkout)
# -----------------------------------------------------------------------------
#  We only touch the DEFAULT clone location. If REPO_DIR points at a checkout
#  the script was launched from, we leave that working tree strictly alone.
echo "[5/7] Preparing the repository ..."
if [ "$REPO_DIR" = "$DEFAULT_REPO_DIR" ]; then
    if [ -e "$REPO_DIR/.git" ]; then
        echo "      updating existing checkout ..."
        git -C "$REPO_DIR" pull --ff-only
    else
        echo "      cloning into \"$REPO_DIR\" ..."
        git clone "$REPO_URL" "$REPO_DIR" || die "could not clone $REPO_URL. Check your network connection and retry."
    fi
fi
[ -e "$REPO_DIR/.git" ] || die "no repo checkout found at $REPO_DIR."


# -----------------------------------------------------------------------------
#  [6/7]  Make sure any running herdr server is COMPATIBLE
# -----------------------------------------------------------------------------
#  A server from an older herdr build still answers "status: running" but also
#  "private_protocol_compatible: no"; every pane/agent API call against it fails
#  with protocol_mismatch, so stop it and let step 7 start a fresh one. We match
#  private_protocol_compatible specifically: the status output also carries an
#  "endpoint_compatible:" line that a bare "compatible: yes" would match.
echo "[6/7] Checking the herdr server ..."
server_running()    { "$HERDR" status server 2>/dev/null | grep -iq "status: running"; }
server_compatible() { "$HERDR" status server 2>/dev/null | grep -iq "private_protocol_compatible: yes"; }

SERVER_READY=""
if server_running; then
    if server_compatible; then
        SERVER_READY=1
    else
        echo "      restarting outdated herdr server (protocol mismatch) ..."
        "$HERDR" server stop >/dev/null 2>&1
    fi
fi


# -----------------------------------------------------------------------------
#  [7/7]  Launch Claude as a tracked pane inside herdr
# -----------------------------------------------------------------------------
#  launch_claude_pane creates a labelled tab in the "Curl" workspace (creating
#  the workspace if needed), anchored to the repo with CLAUDE_MODEL forwarded
#  into its environment, then types the claude command into its shell so herdr's
#  integration hook detects and tracks it. Every value reaches node through the
#  environment, never pasted into the script text, so a path or label with a
#  quote in it cannot break out.
launch_claude_pane() {
    # Incrementing label ("Claude - Curl", "Claude 2 - Curl", ...) from the
    # number of claude agents herdr already tracks. Display-only.
    local count
    count="$("$HERDR" agent list 2>/dev/null | node -e '
        let s = ""; process.stdin.on("data", d => s += d).on("end", () => {
            try { const a = JSON.parse(s).result.agents || [];
                  console.log(a.filter(x => x.agent === "claude").length); }
            catch { console.log(0); } });')"
    local n=$(( ${count:-0} + 1 ))
    local repo_name; repo_name="$(basename "$REPO_DIR")"
    if [ "$n" -eq 1 ]; then AGENT_LABEL="Claude - $repo_name"; else AGENT_LABEL="Claude $n - $repo_name"; fi
    export AGENT_LABEL

    local pane_id
    pane_id="$(node -e '
        const { execFileSync } = require("child_process");
        const h = process.env.HERDR, e = process.env;
        const run = (...a) => JSON.parse(execFileSync(h, a, { encoding: "utf8" }));
        const env = "CLAUDE_MODEL=" + e.CLAUDE_MODEL;
        const ws = (run("workspace", "list").result.workspaces || [])
            .find(w => w.label === e.HERDR_WORKSPACE_LABEL);
        let r;
        if (ws) {
            r = run("tab", "create", "--workspace", ws.workspace_id, "--cwd", e.REPO_DIR,
                    "--env", env, "--label", e.AGENT_LABEL, "--focus").result;
        } else {
            r = run("workspace", "create", "--cwd", e.REPO_DIR, "--env", env,
                    "--label", e.HERDR_WORKSPACE_LABEL, "--focus").result;
            execFileSync(h, ["tab", "rename", r.tab.tab_id, e.AGENT_LABEL]);
        }
        execFileSync(h, ["workspace", "focus", r.root_pane.workspace_id]);
        console.log(r.root_pane.pane_id);
    ')"
    [ -n "$pane_id" ] || { echo "ERROR: herdr could not create a pane for Claude."; return 1; }

    # Wait for the pane's shell to print a prompt ($, %, # or >) so we do not
    # type into a shell that has not finished starting.
    "$HERDR" pane wait-output "$pane_id" --regex '[$%#>]' --timeout 15000 >/dev/null 2>&1
    "$HERDR" pane send-text "$pane_id" "claude --model $CLAUDE_MODEL --dangerously-skip-permissions --verbose" \
        && "$HERDR" pane send-keys "$pane_id" Enter \
        || { echo "ERROR: herdr could not launch Claude in the pane."; return 1; }
}

echo "[7/7] Launching Claude in herdr ..."
if [ -n "$SERVER_READY" ]; then
    launch_claude_pane || die "could not start Claude in herdr."
    echo
    echo "Attaching to the herd (detach with the herdr prefix, Ctrl+B then d) ..."
    exec "$HERDR"
fi

# No server yet: `herdr` in the foreground starts one and attaches this
# terminal. A background job waits (up to ~20s) for it to accept API calls --
# server running AND `pane list` returning a pane -- then creates Claude's pane.
LOG="${TMPDIR:-/tmp}/hrdrClaudeNative.$$.log"
(
    for _ in $(seq 1 20); do
        if server_running && "$HERDR" pane list 2>/dev/null | grep -iq "pane_id"; then
            launch_claude_pane
            exit $?
        fi
        sleep 1
    done
    echo "ERROR: herdr did not become ready in time."
    exit 1
) >"$LOG" 2>&1 &

echo "      starting a herdr session (Claude's pane appears in a moment; log: $LOG) ..."
exec "$HERDR"
