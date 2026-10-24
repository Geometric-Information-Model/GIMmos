#!/usr/bin/env python3
"""Install the GitHub CLI (gh) for the current OS.

Strategy per OS:

- Windows: winget install --id GitHub.cli
- Linux (apt): official GitHub apt repo sequence, then apt install gh (sudo)
- macOS: brew install gh

Post-install: gh extension install agynio/gh-pr-review (default), then
gh --version and gh extension list. Stdlib only.

Usage:
    python install_gh.py            # install gh + gh-pr-review extension
    python install_gh.py --dry-run  # print the commands without running them
    python install_gh.py --no-extension  # install gh only, skip gh-pr-review
"""

from __future__ import annotations

import argparse
import platform
import shutil
import subprocess
import sys


def run(command: list[str], *, dry_run: bool) -> int:
    """Run a command (or print it under --dry-run). Return its exit code."""
    printable = " ".join(command)
    if dry_run:
        print(f"[dry-run] {printable}")
        return 0
    print(f"$ {printable}")
    return subprocess.run(command, check=False).returncode


def install_windows(*, dry_run: bool) -> int:
    return run(
        ["winget", "install", "--id", "GitHub.cli", "--accept-package-agreements", "--accept-source-agreements"],
        dry_run=dry_run,
    )


def install_linux(*, dry_run: bool) -> int:
    if not shutil.which("apt-get"):
        print("apt-get not found. See https://cli.github.com/manual/installation")
        return 1

    commands = [
        ["sh", "-c", "type -p wget >/dev/null || (sudo apt update && sudo apt install wget -y)"],
        ["sudo", "mkdir", "-p", "-m", "755", "/etc/apt/keyrings"],
        [
            "sh",
            "-c",
            "out=$(mktemp) && wget -nv -O$out "
            "https://cli.github.com/packages/githubcli-archive-keyring.gpg "
            "&& cat $out | sudo tee /etc/apt/keyrings/githubcli-archive-keyring.gpg > /dev/null",
        ],
        ["sudo", "chmod", "go+r", "/etc/apt/keyrings/githubcli-archive-keyring.gpg"],
        ["sudo", "mkdir", "-p", "-m", "755", "/etc/apt/sources.list.d"],
        [
            "sh",
            "-c",
            'echo "deb [arch=$(dpkg --print-architecture) '
            "signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] "
            'https://cli.github.com/packages stable main" '
            "| sudo tee /etc/apt/sources.list.d/github-cli.list > /dev/null",
        ],
        ["sudo", "apt", "update"],
        ["sudo", "apt", "install", "gh", "-y"],
    ]
    for command in commands:
        if run(command, dry_run=dry_run) != 0:
            return 1
    return 0


def install_macos(*, dry_run: bool) -> int:
    if not shutil.which("brew"):
        print("Homebrew not found. See https://cli.github.com/manual/installation")
        return 1
    return run(["brew", "install", "gh"], dry_run=dry_run)


def install_extension(*, dry_run: bool) -> int:
    return run(["gh", "extension", "install", "agynio/gh-pr-review"], dry_run=dry_run)


def verify(*, dry_run: bool, with_extension: bool) -> None:
    if dry_run:
        print("[dry-run] gh --version")
        if with_extension:
            print("[dry-run] gh extension list")
        return
    gh = shutil.which("gh")
    if not gh:
        print("\ngh is not yet on PATH in this shell. Open a new shell or restart your terminal.")
        return
    print()
    subprocess.run([gh, "--version"], check=False)
    if with_extension:
        subprocess.run([gh, "extension", "list"], check=False)
    print(
        "\nNext: authenticate once with "
        "'gh auth login --git-protocol https --scopes \"repo,read:org,workflow\" --web', "
        "then verify with 'gh auth status'.",
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="print the install commands without executing them",
    )
    parser.add_argument(
        "--no-extension",
        action="store_true",
        help="skip installing agynio/gh-pr-review (installed by default after gh)",
    )
    args = parser.parse_args(argv)

    system = platform.system()
    print(f"Detected OS={system!r}")

    if system == "Windows":
        result = install_windows(dry_run=args.dry_run)
    elif system == "Linux":
        result = install_linux(dry_run=args.dry_run)
    elif system == "Darwin":
        result = install_macos(dry_run=args.dry_run)
    else:
        print(f"Unsupported OS: {system}. See https://cli.github.com/manual/installation")
        return 1

    if result != 0:
        return result

    with_extension = not args.no_extension
    if with_extension:
        if install_extension(dry_run=args.dry_run) != 0:
            return 1

    verify(dry_run=args.dry_run, with_extension=with_extension)
    return 0


if __name__ == "__main__":
    sys.exit(main())
