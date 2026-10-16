# ChatGPT-first local development

Use ChatGPT Web to analyze a small, relevant source bundle and produce a downloadable
unified Git `.patch`. Local scripts apply it, run the same checks as CI, and publish
reviewed changes. No gameplay source is changed by these tools.

## Requirements

- Windows PowerShell 5.1 or newer, Git, .NET SDK 10.0.x.
- This repository, with origin pointing to `KHUOCHONGTRAN123/CatDom-LevelGenerator` on GitHub.
- For publication: GitHub CLI (`gh`) and authentication. If needed, install GitHub CLI,
  then run `gh auth login --hostname github.com --git-protocol https --web`.
  The standard Program Files and per-user install locations are detected too.
- Access to remote `develop`. Scripts never update local main/develop branches.

Scripts resolve paths from their own directory; spaces and Unicode in the checkout
path are supported. Run the BAT files by double-clicking or from a terminal. They
pause at the end so the result stays visible. PowerShell invocation is preferable
for automation because it returns immediately with the actual exit code.

## Apply a downloaded patch

1. Ask ChatGPT for a **unified Git diff**, based on the latest remote develop.
   Supply exact relevant files, their paths, your requirements, and failing logs.
   Ask for add/modify/delete text changes, with normal `a/` and `b/` path prefixes.
2. Download the `.patch` outside the checkout (for example, Downloads). A patch
   saved as an untracked repository file would make the clean-tree check fail.
3. Run `Apply-ChatGPT-Patch.bat` and enter its path, or drag the file onto the BAT.
   Alternatively:

   ```powershell
   .\Tools\chatgpt-workflow\Apply-ChatGPT-Patch.bat "C:\Users\Me\Downloads\m5-1.patch"
   ```

4. Enter a new `feature/*` branch name, or accept the timestamped suggestion.
   The script fetches develop, creates the branch from it, checks the whole patch,
   and applies it atomically using `git apply --check` followed by `git apply`.
5. Review `git status --short`, `git diff --stat`, and `git diff`.
   Changes remain uncommitted, even after successful validation.

Patches touching these tools or `.github/workflows/` require typing `ALLOW`.
Only apply patches you have reviewed and trust: the tool never invokes a script
from the patch directly, but .NET builds can execute MSBuild tasks and the test
harness executes code. A patch is code, not a security sandbox.

## Publish reviewed changes

Run `Publish-ChatGPT-Changes.bat`. Select explicit file numbers from the list.
All uncommitted changes must be selected or separately stashed so validation
matches the published code. An existing staged index must be reviewed and cleared
first (`git restore --staged -- <path>` keeps edits). Generated/output directories
and credential files cannot be selected, even if tracked. Ignored untracked files
are never offered. The scripts scan selected contents and outgoing history for
common secret patterns, but this is a heuristic: review the diff for credentials.

Publication requires feature/*, verifies origin/develop, and requires develop to
be an ancestor of HEAD. If develop advanced, integrate it deliberately and rerun.
The tool reruns restore/build/tests, stages explicit paths, scans the outgoing
branch changes, displays the staged summary, asks for commit message, PR summary,
and risks, then requires typing `PUBLISH`. It commits, pushes without force, and
creates a PR targeting develop (or returns the existing open PR for that branch).
An existing PR description is retained; review/update it if scope has changed.
No merge, release tag, or history rewrite is performed.

## Validation and logs

Both scripts use the current CI commands:

```powershell
dotnet restore Solver.slnx
dotnet build Solver.slnx -c Release --no-restore -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false
dotnet run --project LevelGenerator.Tests -c Release --no-build --no-restore
dotnet Verification/bin/Release/net10.0/Solver.Verification.dll v3-countdown-oracle-tests .
```

Logs are stored in ignored `artifacts/chatgpt-workflow/<timestamp>/`, with one log
per command and its exit code. Restore/build failure skips dependent steps;
harness failure still allows the independent oracle to run. Any failure returns
nonzero and prevents publication. A printed PASS inside a failing process does
not override its exit code. Local PASS does not assert GitHub Actions success;
CI review remains required. Long generation benchmarks are not run.

## Dry-run and help

```powershell
.\Tools\chatgpt-workflow\Apply-ChatGPT-Patch.ps1 -PatchPath C:\Downloads\change.patch -DryRun
.\Tools\chatgpt-workflow\Publish-ChatGPT-Changes.ps1 -Files README.md,docs/design.md -DryRun
.\Tools\chatgpt-workflow\Apply-ChatGPT-Patch.ps1 -Help
.\Tools\chatgpt-workflow\Publish-ChatGPT-Changes.ps1 -Help
```

Dry-run performs read-only prerequisite, repository, remote availability, path,
and selection checks and prints the commands. It does not fetch, write logs,
create branches, apply, stage, build, commit, push, or open PRs. Apply dry-run
checks syntax but does **not** establish applicability to freshly fetched develop;
that check requires the actual run. Publish dry-run does not claim tests passed.

## Conflicts, failures, and recovery

- Invalid/inapplicable patches fail without partial application; a newly created
  feature branch may remain. Inspect `git status`. Send ChatGPT the Git error,
  current develop commit (`git rev-parse origin/develop`), and exact relevant source.
  Ask for a replacement patch against those bytes. Do not silently use `--reject`.
- After failed tests, changes and logs remain. Ask ChatGPT for a correction using
  failing command, exit code, and relevant log excerpts. Review and commit or stash
  the first patch before using Apply again (it requires a clean tree). You may
  manually apply a reviewed follow-up on the same feature branch with
  `git apply --check <patch>` then `git apply <patch>` and rerun Publish.
- To save work and switch away: inspect status, run `git stash push -u`, verify the
  stash using `git stash list`, then switch branches. Ignored logs stay in place.
  Restore later with `git stash apply` and inspect before dropping the stash.
- After cancellation/security rejection following staging, inspect the staged
  diff, then `git restore --staged -- <path>` to preserve and unstage edits.
- If commit succeeded but push/PR failed, retain the commit. Retry
  `git push -u origin HEAD`, then `gh pr list --head <branch> --base develop`.
  If no PR exists, use `gh pr create --base develop --head <branch> --title <title>
  --body-file artifacts/chatgpt-workflow/pr-description.md` if the body exists,
  otherwise prepare a description with summary, files, validation, risks, and CI
  review requirement. Publish refuses an empty worktree instead of adding an
  empty recovery commit.
- To inspect stable `v0.1-m4.3` without disturbing work:

  ```powershell
  git fetch origin --tags
  git show --no-patch v0.1-m4.3
  git worktree add --detach "../CatDom stable review" v0.1-m4.3
  ```

  If the tag is absent, stop and ask for the correct checkpoint; never fabricate
  or replace it. Create a separate recovery branch/worktree when resuming from
  that tag. Do not reset/clean away current work.

## Complete example

On clean develop, give ChatGPT the current evaluation contract files and ask for
an M5.1 contract-only patch against the supplied commit, with no gameplay changes.
Download `m5-1-contracts.patch` to Downloads. Drag it onto Apply, enter
`feature/chatgpt-m5-1-contracts`, and inspect the resulting diff and logs. If every
check passes, run Publish, select the contract and related test/documentation files,
enter `feat(evaluation): add gameplay evaluation contracts`, describe the contracts
and their compatibility risks, and type `PUBLISH`. Open the returned PR targeting
develop and wait for actual CI completion and human review. If the baseline
harness fails, publication stops; supply its logs to ChatGPT and resolve the
failure separately instead of bypassing checks.

## Limitations and maintenance

Only ordinary UTF-8/ASCII unified Git text patches are supported. Binary, rename,
copy, mode-only, executable, symlink, submodule, quoted/escaped Git filenames,
control-character paths, Windows alternate streams, and reparse-point targets
are rejected. Represent renames as deletion/addition. Lines must match the base;
patches cannot encode ignored local assets or a whole development environment.
Git's normal context matching applies; no fuzzy/reject fallback is used.

The command sequence is centralized in `Workflow.Common.ps1`; update it deliberately
if CI changes. The scripts require origin's canonical HTTPS/SSH identity and an
ordinary feature branch. They do not support alternate push URLs or multiple origins.
No token is requested or printed by these tools. PR metadata and tests are local
claims pending CI review; secret detection cannot guarantee absence of secrets.

Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
Tools/chatgpt-workflow/Test-Workflow.ps1` for isolated regression tests. Real Git
operates on temporary fixtures; dotnet/gh are mocked. Fixtures and logs are retained
under the printed temp directory for inspection. These tests do not push to GitHub.
