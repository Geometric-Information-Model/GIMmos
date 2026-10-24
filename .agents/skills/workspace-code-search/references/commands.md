# Semble command reference (GIM workspace)

Run every command from the **GIMmos** repository root with **`uv run`**.

`path` defaults to the current directory when omitted. Git URLs are accepted.

## search

Find code by describing behavior or naming a symbol:

```bash
uv run semble search "authentication flow" ./my-project
uv run semble search "save_pretrained" ./my-project
uv run semble search "save model to disk" ./my-project --top-k 10
```

Cross-repo (typical GIM workspace):

```bash
uv run semble search "authentication flow" ../CoreWebApi
uv run semble search "map coordinate search" ../GIMfront --top-k 5
```

### Content filters

```bash
uv run semble search "deployment guide" ./my-project --content docs
uv run semble search "database host port" ./my-project --content config
uv run semble search "authentication" ./my-project --content all
```

- `docs` — documentation and prose (`AGENTS.md`, ledgers, skills, README).
- `config` — yaml, toml, and similar config files.
- `all` — code, docs, and config.

## find-related

Discover code similar to a known location (use `file_path` and `line` from a prior search result):

```bash
uv run semble find-related src/auth.py 42 ./my-project
uv run semble find-related GIMtransfer/FileManager/FileManagerInternal.cs 162 .
```

## Index behavior

The index is built on first run, cached for subsequent runs, and invalidated automatically when files change.

## Fallback without uv project

```bash
uvx --from "semble[mcp]" semble search "…" ./path
```

Prefer `uv run semble` when working in GIMmos (locked dependency in `pyproject.toml`).

## CodeGraph and ast-grep

Run from the GIMmos root. The `--` right after the binary name is required. npm otherwise treats later flags as its own, and a query aimed at another repo stays in GIMmos.

```bash
npm exec --no -- codegraph -- explore "<query>" --path ../CoreWebApi
npm exec --no -- codegraph -- impact <symbol> --path ../GIMmanager
npm exec --no -- codegraph -- sync --quiet <repo-path>
npm exec --no -- codegraph -- init --yes <repo-path>
npm exec --no -- ast-grep -- run --pattern '<shape>' --lang js ../GIMfront
```

`--path` is for `explore` and `impact`. `sync` and `init` take the repo path as a positional argument. Omit `--path` when the target is GIMmos itself.
