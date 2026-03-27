# AGENTS Instructions

These instructions apply to the repository rooted at `C:\Users\dculv\Documents\markdown-bei-nacht`.

## Editing Rule

Temporary rule:

- Do not use `apply_patch` for now.
- Use PowerShell-based file edits instead.

Reason:

- `apply_patch` is currently unreliable in this Windows workspace.
- This rule should be removed once the Windows `apply_patch` issue is fixed.

## Code Change Rules

- Keep changes surgical and scoped to the active request.
- Prefer updating existing files and docs before adding new files.
- Avoid unrelated refactors while addressing a task.
- Do not edit archived assets for active delivery; revive by copying into the active lane.

## Git Workflow

- Never use `git add -A`.
- Stage files and folders explicitly with path-based `git add` commands.
- Confirm staged and unstaged state with `git status --porcelain` before committing.
- Do not amend or rewrite commits unless explicitly requested.

## Commit Message Format

- For multiline `git commit` messages, always use a Bash heredoc.
- Never use escaped `\n` sequences inside `-m`.

Example:

```bash
git commit -F - <<'MSG'
feat(scope): short summary

- First detail
- Second detail
MSG
```

## Build Workflow

- Do not run `scripts/publish.ps1` and `scripts/build-installer.ps1` in parallel.
- These scripts share the same publish output under `artifacts/publish/win-x64` and can collide on Windows file locks.
- If both artifacts are needed, run them sequentially.
- If the installer is needed and `build-installer.ps1` already performs the required publish step, prefer running only the installer script.

## Documentation And Changelog Policy

- When behavior, interfaces, defaults, or workflows change, update the nearest relevant `README.md` in the same scope.
- If a scoped changelog exists, update release notes according to that area's policy.
- Do not create new governance docs such as `CONTRIBUTING.md` unless explicitly requested.

## Safety Guardrails

- Never run destructive commands such as `git reset --hard`, mass deletion, or force pushes unless explicitly requested.
- Never revert or discard user-authored changes you were not asked to change.
- Surface blockers and assumptions early when they affect correctness.

## Pre-Commit Checklist

- Verify only intended files are staged.
- Verify docs were updated where behavior changed.
- Verify commit message reflects actual scope and impact.

## Working Style

- Prefer `rg` and `rg --files` for fast search when available.
- Keep edits ASCII unless a file already requires another encoding.
- Avoid destructive git commands unless the user explicitly asks for them.
- Treat unrelated local changes as user-owned unless clearly created for the current task.

## Repo Notes

- This repo contains the Windows desktop Markdown viewer `Markdown bei Nacht`.
- Build helper scripts live under `scripts`.
- Installer assets and the Inno Setup script live under `installer`.
- The current project includes workspace-local SDK and cache folders that should not be committed.

