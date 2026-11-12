# Geometry and story validation

Run from the repository root:

```powershell
dotnet run --project Validation/Geometry/GeometryValidation.csproj --configuration Release
dotnet run --project Validation/CutPull/CutPullValidation.csproj --configuration Release
```

Requires .NET 10 SDK and the Unity editor version recorded in [ProjectVersion.txt](../../ProjectSettings/ProjectVersion.txt). The shared [build configuration](../Directory.Build.props) reads that version automatically and uses the standard Windows Unity Hub directory (`C:/Program Files/Unity/Hub/Editor`), so the commands above need no editor-path argument on a standard installation. For a custom Hub directory, append `-p:UnityHubEditorRoot="D:/Unity/Hub/Editor"`; for another installation layout or operating system, append `-p:UnityManaged="path/to/Editor/Data/Managed/UnityEngine"`. An explicit `UnityManaged` value takes precedence. Missing engine references produce an error explaining how to set the path. This only locates installed editor assemblies; it does not change the project's Unity version or add Unity or NuGet dependencies.

The runner compiles `Assets/Via/Scripts` and explicitly includes `Assets/Via/Sandbox/Scripts` against the installed Unity engine modules. It reports its current check count at runtime. Checks cover geometry, growth, tools, the formal first level's tutorial and both playable responses, the level 2–7 target/paradigm configuration, and the production CSV story in `Assets/Via/Data/Story/Tables`:

- The five production tables (`Flow`, `Dialogue`, `Presentation`, `LevelResults`, `AudioCues`) load and validate through the real `StoryTableLoader`.
- All eight registered levels (`F007/F161/F287/F438/F501/F535/F753/F868`) are reachable from the shared entry `F001` and each starts a minigame session with its expected `LevelId`; completing them walks act 8 silently into act 9 and reaches one of the three true endings (`F1032`/`F1033`/`F1034` carrying `T_END_A`/`T_END_B`/`T_END_C` by the eighth-act result).
- The act-1 closing chain (`F010` wrap-up handing over to the teacher dialogue `T008`–`T019`, `F023` presenting the final scene fade `P007`, then off to `F101`) clears every mouse layer, and the act-2 cake chain (`F160` presenting `P103` right before the level, `F180` presenting the eaten-cake `P104` on the like branch, `F181` presenting `P105` back in the cafe) clears every cake layer, so no act-1/act-2 layer can leak into later acts.
- The act-4 advertisement chain (`F316`→`F328` presenting `P316`/`P317`/`P318`/`P319`, handing off to `F401`) is registered between the act-3 hospital and act 5; the zoom dissolves from the `advertisement` TV shot into the fullscreen `advertisement(2)` layer, which the final fade clears.
- The audio cue table covers the 25 designed beat nodes; the music ladder (`soft`→`horror1`/`ad`→`horror2`→`soft`→`soft_end`) and ambience beds (`amb_cafe`/`amb_hospital`/`amb_tv`/`amb_rain`/`amb_lab`/`none`) follow the per-act plan, and every cue id and music slot resolves through the scene `AudioLibrary` to a wav that shipped with an import meta.
- Story checks use the production loader and `GameFlow` with explicit minigame/presentation test adapters; scene references are checked from the saved scene YAML.

The first-level and late-game gameplay assertions that referenced the older outline node/build (prototype levels, candy teaching, opponent callbacks, ending restoration) were superseded when acts 5–9 were rebuilt and are not part of the current suite; `StoryIntegrationChecks.cs` documents the current checks and can be expanded against the existing API.

It does not launch Unity or assert UI rendering, actual Inspector deserialization, teaching snapshot restoration through MonoBehaviour lifecycle, Play-mode interactions, or a platform Build. Inspector-field warnings from standalone compilation are expected.
