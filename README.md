# IIT Kharagpur Department Fire Safety VR

A Unity fire-response learning and assessment prototype based on the Industrial and Systems Engineering department at IIT Kharagpur. Play with keyboard and mouse on Windows, or use **Meta Quest 3 controllers through PC Link**.

This is a student project, not an official IIT Kharagpur product or a certified fire-safety simulator. Building dimensions and some room details are provisional.

## What it does

- **Theory & Quiz:** narrated lessons on PASS, the represented fire hazards, equipment and evacuation decisions, followed by a practice quiz.
- **Configurable play:** choose Easy/Medium/Hard, ordinary/electrical/liquid fire, initial intensity, location and floor. Random mode draws a new combination independently; repeats are possible.
- **Developing incidents:** flames and smoke can grow into the corridor and another open room, including obstruction of the only staircase.
- **A populated department:** 12 students, teachers and staff begin with role-specific activities and respond to visible fire or the alarm.
- **Evacuation:** one staircase and two ground-floor exits, with NPC selection of the nearest available exit before continuing to assembly.
- **Equipment:** an extinguisher with PASS observations, alarm and assistance panels, doors, taps, coolers and refillable buckets.
- **Detailed analysis:** post-session decisions, equipment observations, route evidence, recommendations and a timeline. The latest result is saved locally.
- **Work-system analysis:** two read-only report pages show observed response times and the recorded PASS handling sequence. Missing, reversed and interrupted milestones are identified explicitly; these are observed durations, not standard times or a formal PMTS result.
- **Controller support:** ray and nearby grabbing, 30-degree snap turns, joystick dead zones, haptic feedback and tracking-loss recovery.

Assessment play provides no live correctness hints. The theory quiz is separate from assessment scoring.

## Open and run

1. Install **Unity 2022.3.21f1** through Unity Hub and activate an eligible Unity license.
2. Clone this repository, then use **Unity Hub > Add project from disk** and select the repository root. Do not create a new empty project inside it.
3. Let Unity restore the pinned packages and import the assets. Open **`Assets/Scenes/FireSafetyTraining.unity`**.
4. Select the **Game** tab and press **Play**, with Pause off. Without an active headset, the footer should say **Desktop mode**.
5. Start with **CONTROLS** and **SOUND CHECK**, then **THEORY & QUIZ** or **PLAY**.

For a clearer editor preview, select **Full HD (1920×1080)** in Game view, turn off **Low Resolution Aspect Ratios**, and maximize the Game panel with **Shift+Space**.

For Quest 3, connect through Meta Horizon Link and enter the PC Link environment **before** pressing Play or launching the Windows build. The footer should indicate that the headset and both controllers are tracked. This repository does not include a standalone Quest APK.

## Controls

| Action | Desktop | Quest controllers |
|---|---|---|
| Walk | WASD | Left stick |
| Look / turn | Hold right mouse and move | Head tracking / right-stick snap turn |
| Menu / door / alarm / assistance / tap | Click menu; aim and press E in the world | Right ray + right trigger |
| Pick up / return equipment | G | Tap right grip; tap again to return |
| Remove extinguisher pin | P | Left trigger |
| Spray / pour | Hold left mouse | Hold right trigger |
| Session controls | Tab | Left Y / supported menu button |
| Release cursor | Esc | — |

The extinguisher cylinder follows the left hand and the nozzle follows the right. Release the trigger after grabbing or closing a panel before spraying/pouring. Equipment is returned to its station when released. The XR simulator is off by default.

## Build

With Unity closed, run this in PowerShell from the repository root:

```powershell
.\Tools\Run-UnityChecks.ps1 -Check WindowsXRBuild
```

The Windows executable is written to **`Builds/WindowsXR/DepartmentFireSafety.exe`**. Keep the entire build folder together. This route configures the saved scene and builds it without regenerating the department.

**Fire Safety > Build Complete Assessment Environment** regenerates and replaces the scene. Save any hand-edited layout separately before using it. Android builds require Android Build Support, SDK/NDK/OpenJDK and separate on-device verification.

## Documentation

- [Setup, builds and troubleshooting](Docs/SETUP.md)
- [Scenario and assessment behavior](Docs/SCENARIOS.md)
- [Code structure and architecture](Docs/ARCHITECTURE.md)
- [Tests and validation limits](Docs/TESTING.md)
- [Building reference and known limitations](Docs/BUILDING_REFERENCE.md)
- [Work-system analysis and interpretation](Docs/COURSE_ANALYSIS.md)
- [Latest source changes](CHANGELOG.md)
- [Keeping the project after uninstalling the tools](Docs/ARCHIVING.md)
- [Third-party credits and licensing](THIRD_PARTY_NOTICES.md)

## Repository scope

Included: current scene, custom runtime/editor/test code, required materials, meshes, audio, controller-model dependencies, Unity package manifests, project settings and build/check tools. Unity `.meta` files are preserved.

Excluded: `Library`, temporary data, logs, builds, executables, IDE files, personal department photographs/videos, participant reports, unused legacy scenes and unrelated imported asset packs. Unity downloads its package dependencies on first import. Generated materials and the saved scene are included because they are required authoring assets, not Unity's disposable cache.

The public source snapshot uses a small procedural fire-crackle loop in place of the working folder's unrelated imported fire-audio asset. It retains the synthesized alarm. This keeps the public project independent of that Asset Store pack; see the licensing notes.

## Validation status

The working version passed four focused controller PlayMode checks, two controller-policy EditMode checks and the 23 managed assessment-rule checks. The report update adds four passing course-analysis EditMode tests and one passing report-navigation PlayMode test; the four controller regressions also passed after that update. These were focused runs, not a claim of one combined full-suite run. Selected results are summarized in [the validation record](Docs/validation-results.json). The project owner reported successful VR use of an earlier version and confirmed that the report update worked in Unity.

The report update has no newly documented physical Quest trial, and the public source copy has not completed a fresh-clone Unity import. Headset frame rate, comfort, stereo readability, floor height, haptics and physical audio are not certified by these automated checks.

## Credits

The original project was **Fire Extinguisher Training / FireEx VR**, developed by **Oasis Uniandes**. The existing MIT copyright and license are preserved in [LICENSE](LICENSE). The department-specific learning, assessment, evacuation and controller implementation is under `Assets/FireSafety`.

Unity XR sample components and controller models remain subject to their separate Unity Companion License. Package dependencies retain their own licenses. This project does not claim that third-party content is covered by the root MIT license.
