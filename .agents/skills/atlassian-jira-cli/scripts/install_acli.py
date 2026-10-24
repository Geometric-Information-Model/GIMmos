#!/usr/bin/env python3
"""Install the Atlassian CLI (acli) for the current OS without ever editing PATH.

Strategy per OS (all land acli on the default PATH, or fail loudly with a manual
note - this script never modifies any PATH environment variable):

- Windows: download acli.exe into %LOCALAPPDATA%\\Microsoft\\WindowsApps (on PATH
  by default, no admin needed).
- Linux (apt): add the Atlassian apt repo + key, then apt install acli (sudo).
- Linux (yum): add the Atlassian rpm repo, then yum install acli (sudo).
- macOS: brew tap/install if brew exists, else curl the binary to /usr/local/bin.

If acli cannot be placed on PATH (no privileges / no package manager), the binary
is still downloaded where possible and the script prints the install path plus a
"add this to PATH yourself" note. Stdlib only.

Usage:
    python install_acli.py            # install for the detected OS/arch
    python install_acli.py --dry-run  # print the commands without running them
"""

from __future__ import annotations

import argparse
import os
import platform
import shutil
import subprocess
import sys
import urllib.request
from pathlib import Path


WINDOWS_URLS = {
    "amd64": "https://acli.atlassian.com/windows/latest/acli_windows_amd64/acli.exe",
    "arm64": "https://acli.atlassian.com/windows/latest/acli_windows_arm64/acli.exe",
}
LINUX_CURL_URLS = {
    "amd64": "https://acli.atlassian.com/linux/latest/acli_linux_amd64/acli",
    "arm64": "https://acli.atlassian.com/linux/latest/acli_linux_arm64/acli",
}
MACOS_CURL_URLS = {
    "amd64": "https://acli.atlassian.com/darwin/latest/acli_darwin_amd64/acli",
    "arm64": "https://acli.atlassian.com/darwin/latest/acli_darwin_arm64/acli",
}


def normalize_arch() -> str:
    """Map platform.machine() onto the 'amd64'/'arm64' tokens acli uses."""
    machine = platform.machine().lower()
    if machine in ("x86_64", "amd64", "x64"):
        return "amd64"
    if machine in ("arm64", "aarch64"):
        return "arm64"
    raise SystemExit(
        f"Unsupported architecture: {machine!r}. Supported: amd64, arm64.",
    )


def run(command: list[str], *, dry_run: bool) -> int:
    """Run a command (or print it under --dry-run). Return its exit code."""
    printable = " ".join(command)
    if dry_run:
        print(f"[dry-run] {printable}")
        return 0
    print(f"$ {printable}")
    return subprocess.run(command, check=False).returncode


def download(url: str, destination: Path, *, dry_run: bool) -> None:
    """Download url to destination (or print the intent under --dry-run)."""
    if dry_run:
        print(f"[dry-run] download {url} -> {destination}")
        return
    destination.parent.mkdir(parents=True, exist_ok=True)
    print(f"Downloading {url} -> {destination}")
    urllib.request.urlretrieve(url, destination)  # noqa: S310 (trusted vendor URL)


def manual_path_note(install_path: Path) -> None:
    path_directory = install_path.parent
    print(
        f"\nacli was placed at: {install_path}\n"
        f"If 'acli' is not callable from a new shell, add this directory to your "
        f"PATH manually: {path_directory}\n"
        "This installer never edits PATH for you.",
    )


def install_windows(arch: str, *, dry_run: bool) -> int:
    url = WINDOWS_URLS[arch]
    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        print("LOCALAPPDATA is not set; cannot resolve the default WindowsApps dir.")
        return 1
    install_path = Path(local_app_data) / "Microsoft" / "WindowsApps" / "acli.exe"
    download(url, install_path, dry_run=dry_run)
    if not dry_run and not shutil.which("acli"):
        manual_path_note(install_path)
    return 0


def install_linux(arch: str, *, dry_run: bool) -> int:
    if shutil.which("apt-get"):
        commands = [
            ["sudo", "apt-get", "install", "-y", "wget", "gnupg2"],
            ["sudo", "mkdir", "-p", "/etc/apt/keyrings"],
            [
                "sh",
                "-c",
                "wget -qO - https://acli.atlassian.com/gpg/public-key.asc "
                "| sudo gpg --dearmor -o /etc/apt/keyrings/acli-archive-keyring.gpg",
            ],
            [
                "sh",
                "-c",
                'echo "deb [signed-by=/etc/apt/keyrings/acli-archive-keyring.gpg] '
                'https://acli.atlassian.com/linux/deb stable main" '
                "| sudo tee /etc/apt/sources.list.d/acli.list",
            ],
            ["sudo", "apt-get", "update"],
            ["sudo", "apt-get", "install", "-y", "acli"],
        ]
    elif shutil.which("yum"):
        commands = [
            [
                "sudo",
                "yum-config-manager",
                "--add-repo",
                "https://acli.atlassian.com/linux/rpm/acli.repo",
            ],
            ["sudo", "yum", "install", "-y", "acli"],
        ]
    else:
        return install_curl(LINUX_CURL_URLS[arch], dry_run=dry_run)

    for command in commands:
        if run(command, dry_run=dry_run) != 0:
            print("Package install failed; falling back to the curl binary install.")
            return install_curl(LINUX_CURL_URLS[arch], dry_run=dry_run)
    return 0


def install_macos(arch: str, *, dry_run: bool) -> int:
    if shutil.which("brew"):
        for command in (
            ["brew", "tap", "atlassian/homebrew-acli"],
            ["brew", "install", "acli"],
        ):
            if run(command, dry_run=dry_run) != 0:
                print("brew install failed; falling back to the curl binary install.")
                return install_curl(MACOS_CURL_URLS[arch], dry_run=dry_run)
        return 0
    return install_curl(MACOS_CURL_URLS[arch], dry_run=dry_run)


def install_curl(url: str, *, dry_run: bool) -> int:
    """Download a unix binary, chmod +x, and install into /usr/local/bin via sudo."""
    staged = Path.home() / "acli"
    download(url, staged, dry_run=dry_run)
    if not dry_run:
        staged.chmod(0o755)
    target = Path("/usr/local/bin/acli")
    if run(["sudo", "install", str(staged), str(target)], dry_run=dry_run) != 0:
        manual_path_note(staged)
    return 0


def verify(*, dry_run: bool) -> None:
    if dry_run:
        print("[dry-run] acli --version")
        return
    acli = shutil.which("acli")
    if not acli:
        print("\nacli is not yet on PATH in this shell. Open a new shell or add it.")
        return
    print()
    subprocess.run([acli, "--version"], check=False)
    print(
        "\nNext: authenticate once with 'acli jira auth login --web', then verify "
        "with 'acli jira auth status'.",
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="print the install commands without executing them",
    )
    args = parser.parse_args(argv)

    system = platform.system()
    arch = normalize_arch()
    print(f"Detected OS={system!r} arch={arch!r}")

    if system == "Windows":
        result = install_windows(arch, dry_run=args.dry_run)
    elif system == "Linux":
        result = install_linux(arch, dry_run=args.dry_run)
    elif system == "Darwin":
        result = install_macos(arch, dry_run=args.dry_run)
    else:
        print(f"Unsupported OS: {system}. See references/installation.md.")
        return 1

    if result == 0:
        verify(dry_run=args.dry_run)
    return result


if __name__ == "__main__":
    sys.exit(main())
