# Development

## Workspace and requirements

Use .NET SDK 10.0.x and Git. GitHub CLI (`gh`) must be authenticated with `gh auth login --hostname github.com --git-protocol https --web` before repository creation or PR operations. Run commands from this directory.

- `Solver.slnx`: eight build projects.
- `LevelGenerator.Core`, `LevelGenerator.Cli`: generator and batch CLI.
- `LevelGenerator.Tests`: executable assertion harness, not xUnit/NUnit.
- `IndependentSolver/IO/HoleShapes.json`: required shape catalog.
- `Verification`, `Verification/V42Reference`: rule checks and independent replay.
- `Solver.Cli`, `Verification/ConsumerSmoke`: solver CLI and consumer checks.
- `LevelGenerator.Examples`, `Levels`: sample configurations and required reference levels.
- `docs`, `Generate Docs`, milestone documents: design and verification documentation.
- `LevelGenerator.Benchmarks`: small tracked baseline; large results belong in ignored output directories.
- `generated`: local output, never committed.

## Build and verification

```powershell
dotnet restore Solver.slnx
dotnet build Solver.slnx -c Release --no-restore -m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false
dotnet run --project LevelGenerator.Tests -c Release --no-build --no-restore
dotnet Verification/bin/Release/net10.0/Solver.Verification.dll v3-countdown-oracle-tests .
```

CI runs these commands on Windows using .NET 10. It does not run long generation benchmarks. The legacy aggregate Verification suite needs fixtures absent from this selected corpus; see `docs/VERIFICATION.md`. Passing the countdown group is not a claim that every historical test passed. Current baseline test failure is recorded in PROJECT_STATUS.md.

## Batch generation (local Windows)

```powershell
dotnet run --project LevelGenerator.Cli -c Release -- batch LevelGenerator.Examples/RegionRelease.quality.request.json generated/m43-local-unique IndependentSolver/IO/HoleShapes.json 20 --workers 10
```

Choose a new, nonexistent output directory each run. Alternatively run `Run-M43-Quality-20.bat`. Certified exports appear at `generated/<batch>/levels/Levelxxxxx.json`. Solver plus independent replay are mandatory; observation scores do not replace gameplay review.

## Branches and pull requests

`main` holds accepted stable milestones. `develop` integrates reviewed work. Start actual milestone implementation from develop; never implement it directly on main. Do not create empty feature branches.

```powershell
git switch develop
git pull --ff-only origin develop
git switch -c feature/m5-1-gameplay-evaluation
# Implement, build, and run the verification commands above.
git status --short
git diff
# Stage explicit files after checking for secrets and generated output.
git add <files>
git commit -m "Implement M5.1 gameplay evaluation"
git push -u origin feature/m5-1-gameplay-evaluation
gh pr create --base develop --title "M5.1 Gameplay Evaluation" --body-file pr-description.md
```

PR descriptions explain resulting behavior, scope, tests and limitations. Review using `gh pr view <number>`, `gh pr diff <number>` and `gh pr checks <number>`. Merge into develop only after human approval. Promote develop to main through a separate reviewed PR after milestone acceptance. Never auto-merge future PRs. These conventions are documented policy; server-side branch protection has not been configured.

## Update local code

Commit or stash local work first; inspect `git status`. Use `git fetch origin --tags`, switch to the desired branch, then `git pull --ff-only`. If histories diverge, inspect and reconcile deliberately; never force push.

## Checkpoint and recovery

`v0.1-m4.3` is reserved until baseline build AND tests pass. It has not been created during setup because the harness fails. Never replace an existing tag.

Once the verified tag exists, inspect it safely in a separate worktree:

```powershell
git fetch origin --tags
git worktree add --detach ../CatDom-m43-review v0.1-m4.3
```

To resume development from the checkpoint, create a real recovery branch with `git switch -c recovery/m43 v0.1-m4.3` after saving current changes. To undo an accepted commit on a shared branch, use a reviewed `git revert <commit>` PR. Avoid destructive reset, history rewriting, or force pushes.
