CatDom Milestone 4.2 - Output Verification FIX3

Root cause:
The StrongVerificationBatchRunner guard refused ANY nonempty destination directory,
even when only unrelated files were present. FIX2 already generated a unique name,
so changing output names again does not solve this class of refusal.

FIX3 C# change:
- LevelGenerator.Core/StrongVerification.cs only refuses overwrite if the output
  contains verification-summary.json, verification-summary.csv, or a
  *.verification.json file. It allows unrelated files but preserves them.
- Tests: an output folder with an unrelated file and subfolder succeeds;
  trying to rerun into an already verified folder fails with IOException.
- The game, level generation, solver, replay, and 25..32 footprint are unchanged.

Install:
1. Close any running verifier.
2. Extract the full source ZIP over your project root. If using the small patch
   ZIP, copy LevelGenerator.Core/StrongVerification.cs and
   LevelGenerator.Tests/Program.cs to those respective project directories.
3. Place Run-M42-Verify-Batch-FIX3.bat beside LevelGenerator.Cli.
4. In PowerShell run: dotnet run --project LevelGenerator.Tests
5. Double click Run-M42-Verify-Batch-FIX3.bat, or drag a batch folder on it.

The BAT checks for the patched source before starting; never deletes old output.

Environment limitation: .NET SDK is not installed on the packaging server;
compiled tests must be run on Windows.
