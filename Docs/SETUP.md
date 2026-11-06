# Setup and troubleshooting

## Requirements

Use Unity **2022.3.21f1**, Windows Build Support, and an activated Unity license. The package manifest pins URP 14.0.10, Oculus XR 4.2.0, XR Management 4.4.1, XRI 3.5.0 and XR Hands 1.8.1, alongside the existing Meta SDK dependencies. Keep `Packages/manifest.json` and `Packages/packages-lock.json` together; Unity restores registry packages on import.

For PC VR, install Meta Horizon Link on the PC and connect a Quest 3 with its controllers. Confirm that the PC/Link connection is usable independently of Unity. The Windows build runs on the PC and streams to the headset; it is not installed on the headset as an APK.

## First launch

Open the repository root in Unity Hub, wait for package import, and open `Assets/Scenes/FireSafetyTraining.unity`. The correct hierarchy contains **Department Fire Safety Assessment** and **XR and Desktop Player**. Select Game and press Play. Keep Pause and Game-view Mute Audio off.

The home menu has three choices: **THEORY & QUIZ**, **PLAY**, **CONTROLS**. Every panel has at most three buttons. Use **CONTROLS > SOUND CHECK** before beginning a trial.

## Quest controllers

Enter PC Link before launching. Keep the existing Windows Oculus XR provider configured. The game owns one camera rig, audio listener, movement system and interaction ray; separately added sample XR Origin rigs are disabled to prevent competing cameras and inputs. Bare-hand gestures are outside this controller configuration.

**Fire Safety > Configure Quest Controller Play** configures the imported controller models and saves the current scene without rebuilding the department. Both controllers must be tracked to begin a VR trial. Tracking loss during a trial suspends timing and equipment use; reconnect, release buttons and centre sticks before resuming.

## Builds and checks

Close Unity before running the command-line helper. Its default editor path can be overridden with `-UnityExe`.

```powershell
.\Tools\Run-UnityChecks.ps1 -Check WindowsXRBuild
.\Tools\Run-UnityChecks.ps1 -Check EditMode
.\Tools\Run-UnityChecks.ps1 -Check PlayMode
```

`WindowsXRBuild` preserves the saved scene and outputs `Builds/WindowsXR`. `WindowsBuild` and `Scene` regenerate the environment. Generated-scene EditMode tests also rebuild it; use a clean checkout or save manual scene edits before those tests.

For standalone Android development, install Unity's Android Build Support components and use `-Check QuestBuild`. That path is present for development, but an APK from it is not a validated standalone release.

## Common symptoms

| Symptom | Check |
|---|---|
| Empty sky or default sample scene | Open FireSafetyTraining in this existing project, rather than an empty nested project. |
| Can't click | Ensure Play is running and Pause is off. In desktop mode, press Esc to release the cursor. |
| XR simulator overlay / tilted desktop view | Disable automatic simulator instantiation in XRI settings, stop Play, then start again. |
| Jagged or tiny editor text | Use Full HD, disable Low Resolution Aspect Ratios, and maximize Game with Shift+Space. |
| Silent voices | Turn off Game-view Mute Audio; check Windows/headset output and volume; use SOUND CHECK. |
| Desktop footer while expecting VR | Confirm Quest Link is active, both controllers are available and the Windows XR provider initializes. |
| Tracking-recovery notice | Restore headset/controller tracking, focus the game, release buttons and centre sticks. |

## Reports

The report opens in game after a trial. Use its page buttons to review overview, scoring evidence, equipment observations, recommendations and timeline. **PLAY > REPORT / BUILDING > LAST REPORT** reopens the latest result.

One `LastAssessment.json` is stored under Unity's `Application.persistentDataPath`; it is not stored in this repository. HTML/CSV export is off by default. Keep participant data and rehearsal results out of Git.
