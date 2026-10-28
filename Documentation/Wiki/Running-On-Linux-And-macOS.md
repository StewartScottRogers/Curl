# Running the dark factory on a remote Linux or macOS machine

The factory's scripts run on Linux and macOS through PowerShell 7 (`pwsh`). This page sets up a
fresh remote machine (Ubuntu or Debian first; macOS with Homebrew where cheap) and shows how to
watch a shift from your desktop.

1. **Get the setup script onto the host.** Over SSH, either clone the repository
   (`git clone https://github.com/StewartScottRogers/Curl.git ~/Curl`) or copy
   `Setup-UnixFactoryHost.sh` to the host. The repository is public, so cloning needs no login.
2. **Run it.** `bash ~/Curl/Setup-UnixFactoryHost.sh`. It installs or verifies git, Node.js, Claude
   Code, herdr (the approach of `hrdrClaudeNative.sh`), the repository clone, the .NET SDK that
   `global.json` pins, PowerShell 7, the GitHub CLI, a `powershell` shim that points at `pwsh`,
   the audio-off file (nobody listens on a remote host), and git's identity. Set
   `GIT_USER_NAME` and `GIT_USER_EMAIL` first and it sets the identity too. It uses `sudo` for
   packages, so run it as a user that has it.
3. **Read the checklist.** It ends with `[x]` for each item present and `[ ]` for each missing one,
   with the command that fixes it, and exits non-zero while any is missing. Running it again is
   safe: it changes nothing that is already in place and says so.
4. **Log in.** These cannot be scripted. Run `claude` and sign in; run `gh auth login`; run
   `gh auth setup-git` so `git push` has credentials. Open a new shell (or `. ~/.profile`) so the
   PATH the script added is in effect, then run the script once more: the checklist should be all `[x]`.
5. **Start a shift.** In a terminal on the host (inside `tmux`, or the herdr session itself):
   `cd ~/Curl && ./RunDarkFactory.sh -NewTab -Lanes Auto -Continuous`.
6. **Watch it from your desktop.** `herdr --remote <ssh-target>`, where `<ssh-target>` is what you
   would give `ssh` (for example `me@factory-host`). Detach with the herdr prefix, then `d`; the
   shift keeps running.
7. **Stop it** with Ctrl+C in the shift's herdr pane, as on the desktop.

The audio-off file is `~/.local/share/Curl/audio-off` on Linux and
`~/Library/Application Support/Curl/audio-off` on macOS (`Get-FactoryDataDir` in
`DarkFactory/Platform.ps1`).
