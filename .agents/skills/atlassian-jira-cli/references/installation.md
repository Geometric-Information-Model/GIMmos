# Installing acli + one-time auth

How to install the Atlassian CLI (`acli`) and authenticate. This file is
**generic** and repo-agnostic. The [`install_acli.py`](../scripts/install_acli.py)
helper automates everything below.

Official install guides:

- [Windows](https://developer.atlassian.com/cloud/acli/guides/install-windows.md)
- [Linux](https://developer.atlassian.com/cloud/acli/guides/install-linux.md)
- [macOS](https://developer.atlassian.com/cloud/acli/guides/install-macos/)

---

## Policy: never edit PATH

The installer only ever places `acli` in a location already on the OS default
PATH (or uses a system package manager that does so). It **never** modifies any
PATH environment variable. If it cannot land `acli` on PATH (no privileges, no
package manager), it downloads the binary, prints the install path, and tells you
to add it to PATH manually.

---

## Quick start

```bash
# 1. install (auto-detects OS/arch; --dry-run prints commands without running)
python .agents/skills/atlassian-jira-cli/scripts/install_acli.py
python .agents/skills/atlassian-jira-cli/scripts/install_acli.py --dry-run

# 2. one-time authentication (interactive browser login)
acli jira auth login --web

# 3. verify
acli jira auth status
```

---

## Per-OS install summary

### Windows (x86-64 / ARM64)

Download the `acli.exe` binary to `%LOCALAPPDATA%\Microsoft\WindowsApps\acli.exe`,
which is on the user PATH by default (no admin, no PATH edit).

- x86-64: `https://acli.atlassian.com/windows/latest/acli_windows_amd64/acli.exe`
- ARM64: `https://acli.atlassian.com/windows/latest/acli_windows_arm64/acli.exe`

### Linux (Debian/Ubuntu — apt)

Adds the Atlassian apt repo and installs the package (lands on PATH, needs sudo):

```bash
sudo apt-get install -y wget gnupg2
wget -qO - https://acli.atlassian.com/gpg/public-key.asc \
  | sudo gpg --dearmor -o /etc/apt/keyrings/acli-archive-keyring.gpg
echo "deb [signed-by=/etc/apt/keyrings/acli-archive-keyring.gpg] https://acli.atlassian.com/linux/deb stable main" \
  | sudo tee /etc/apt/sources.list.d/acli.list
sudo apt-get update && sudo apt-get install -y acli
```

### Linux (RedHat/CentOS — yum)

```bash
sudo yum-config-manager --add-repo https://acli.atlassian.com/linux/rpm/acli.repo
sudo yum install -y acli
```

### Linux (curl fallback)

```bash
# x86-64 (ARM64: replace amd64 with arm64)
curl -fsSL https://acli.atlassian.com/linux/latest/acli_linux_amd64/acli -o acli
chmod +x acli
sudo install acli /usr/local/bin/acli   # or leave in place + add to PATH manually
```

### macOS

Prefer Homebrew if present:

```bash
brew tap atlassian/homebrew-acli
brew install acli
```

Otherwise curl the binary (Apple Silicon: replace `amd64` with `arm64`):

```bash
curl -fsSL https://acli.atlassian.com/darwin/latest/acli_darwin_amd64/acli -o acli
chmod +x acli
sudo mv acli /usr/local/bin/acli        # or add its location to PATH manually
```

---

## Authentication

`acli` requires a one-time interactive login before any `jira` command works:

```bash
acli jira auth login --web      # opens a browser to authenticate
acli jira auth status           # confirms: site, email, auth type
```

This is per-user machine state — a fresh machine is unauthenticated even if
`acli` is installed.
