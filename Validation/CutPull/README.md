# Cut and pull validation

Run from the repository root:

```powershell
dotnet run --project Validation/CutPull/CutPullValidation.csproj --configuration Release
```

Uses .NET 10, with no package dependencies, and the Unity editor version recorded in [ProjectVersion.txt](../../ProjectSettings/ProjectVersion.txt). The shared [build configuration](../Directory.Build.props) reads that version automatically and uses the standard Windows Unity Hub directory (`C:/Program Files/Unity/Hub/Editor`). For a custom Hub directory, append `-p:UnityHubEditorRoot="D:/Unity/Hub/Editor"`; for another installation layout or operating system, append `-p:UnityManaged="path/to/Editor/Data/Managed/UnityEngine"`. An explicit `UnityManaged` value takes precedence. Missing engine references produce an error explaining how to set the path. This locates installed editor assemblies without changing the project version.

Compiles `Assets/Via/Scripts` with the explicitly included `Assets/Via/Sandbox/Scripts` and runs the existing cut, single-end pull, weld, metadata, snapshot, growth and tutorial/configuration checks. The runner reports the current count. Configuration now lives in `Assets/Via/Data/Minigame`. It reuses Geometry/Program.cs with CUT_PULL_VALIDATION; story integration is exercised separately by the Geometry runner.

No Unity lifecycle, mouse interaction, rendering, platform build or full story-flow tests are asserted by this runner. Expected warnings for Inspector-assigned fields can appear in standalone compilation.
