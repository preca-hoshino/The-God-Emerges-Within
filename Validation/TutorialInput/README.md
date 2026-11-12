# Tutorial pointer-capture regression

The project standard requires Unity 6000.3.22f1; the current project records 6000.3.24f1. Use an editor matching the isolated project. This is an Editor-run integration harness, not an NUnit package and not a player component.

Use an isolated copy of the current Unity project (Assets, Packages, ProjectSettings) so the runner cannot change an open working scene, PanelSettings target texture or editor play state. Copy `TutorialInputValidation.cs` into that copy's `Assets/Editor/` directory, then run:

```powershell
& '<Unity editor>/Unity.exe' -batchmode -projectPath '<isolated project>' -executeMethod TutorialInputValidation.RunAfter -logFile '<test log>'
```

The runner opens the real GameFramework scene, waits for the intro, and enters the actual fourth-level story node F438. It sends UI Toolkit pointer events at the tutorial corner with the real world-to-panel transform, both releasing immediately and holding the button until the completion modal appears. It uses mouse pointer down/up to close the modal, rather than keyboard navigation, so a stale pointer capture cannot be hidden by a keyboard-only success.

Coverage: four resolutions, four completion cycles per resolution, actual formal-solve attractor placement after completion, tutorial exit, pause/resume, input disable, pointer cancel, view detach, and normal pull/cut release committing before cleanup. Assert engine pause and input gates return to their expected states and tutorial completion does not write formal records.

Outputs are saved in `<isolated project>/UIExports/TutorialInput_20261019/`. A passing run writes report.json and exits 0; errors write failure.txt and exit 1. No platform build is performed. Actual migration validation is recorded in the [execution report](../../Docs/工程重整执行记录.md).

`RunBefore` is a diagnostic entry for the pre-fix canvas implementation: it expects to reproduce a pointer capture that survives completion and the subsequent mouse-up, writes before-report.json, and exits 0 only when that defect is reproduced. Run it against the old source in a separate isolated copy. It is not the acceptance entry for the fixed code.
