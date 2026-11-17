# Testing and validation

## Recorded results

Selected passing results from the working project are normalized in `validation-results.json`, without machine paths, account details, full editor logs or participant reports.

- 4 focused controller PlayMode tests: ray menu selection, panel movement lock, grip/pin/return, tracking-loss timing/recovery, and obstruction of nearby grabs.
- 2 controller-policy EditMode tests: radial joystick dead zone and analog hysteresis.
- 23 managed assessment-rule checks via `Tools/Verify-Code.ps1`.
- Prior focused smoke/suppression results are listed separately in the JSON record. They are not a claim that every historical test ran together in a single uninterrupted suite.
- On 9 November, four course-analysis EditMode tests passed, all four controller PlayMode regressions passed, and a focused report-navigation PlayMode test passed. The combined controller/report run initially had a report-fixture failure due to missing synthetic descriptions; the corrected focused report run passed. Both outcomes are retained in the validation summary.

The project owner reported successful VR use of an earlier version and confirmed the report update worked in Unity. No new physical Quest performance, haptics, stereo or audio trial is claimed for the report update. The public source copy also substitutes procedural fire audio and removes unrelated assets/settings; a clean Unity import and build of this exact copy still need verification.

## Run checks

Open the project in the pinned Unity editor first so package assemblies are available. Close the editor before invoking a second Unity process.

```powershell
.\Tools\Verify-Code.ps1
.\Tools\Run-UnityChecks.ps1 -Check EditMode
.\Tools\Run-UnityChecks.ps1 -Check PlayMode
python .\Tools\Verify-TrainingAudio.py
python .\Tools\Verify-Repository.py --package-cache .\Library\PackageCache
```

`Verify-Code.ps1` checks managed compilation and the 23 rules; it does not replace Unity scene/play tests. WAV checks verify sample data, not the selected audio output. Test XML and logs are written under ignored `tmp/analysis`.

Generated-scene tests regenerate the scene. Run them in a clean checkout or save hand-edited scene work elsewhere first. Play tests avoid saving a real participant's latest result.

## Headset rehearsal

On the actual Quest 3 and intended PC, check:

1. Enter PC Link, start the app and confirm headset/both-controller tracking in the footer.
2. Check floor height, head movement, walk speed, snap turns and panel readability.
3. Use menus, doors, alarm/call panels and taps with the right ray.
4. Grab/return both equipment types; fill/pour water and perform an extinguisher trial.
5. Interrupt controller tracking and confirm the clock/equipment pause and neutral-input recovery.
6. Hear SOUND CHECK, narration, fire, alarm and spray through the intended headset output.
7. Complete a trial and page through the detailed report and recommendations.
8. Measure frame time and comfort in both small-fire and smoke/spread scenarios. Editor rendering and simulated devices are not substitutes for this check.
