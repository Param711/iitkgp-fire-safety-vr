# Work-system analysis in the completed report

The session report contains two additional pages derived from the data the game already records. They appear after recommendations and before the event timeline. They require no new controller bindings, scene setup or saved-report migration.

## Observed time study

The first page shows trial duration; time from incident start to the first recorded response, alarm, assistance request and source suppression; total spray time; and effective spray time. An absent observation is shown as **Not recorded**, rather than zero.

These are descriptive observations of the simulation trial. They include decisions, movement and pauses. The first-response measure can be triggered by an observed action or movement. No performance rating or allowance is applied, and the application does not produce an industrial standard time.

## Method analysis of PASS

The second page breaks the recorded equipment sequence into four intervals:

| Start milestone | End milestone |
|---|---|
| Extinguisher picked up | PASS: pull |
| PASS: pull | PASS: aim |
| PASS: aim | PASS: squeeze |
| PASS: squeeze | PASS: sweep |

Each interval uses the first valid occurrence of its milestones. A missing milestone produces **Not recorded**; an end before its start produces **Out of order**; a recorded extinguisher release inside the interval produces **Interrupted / released**. The existing PASS observations determine whether the sequence is complete, incomplete, out of order or not assessed.

Established aim includes the existing 0.4-second detection requirement. These intervals cannot isolate pure reach, grasp, aim or mental decision times. They support a task-method breakdown but do not assign predetermined motion codes or a PMTS total. Work sampling, fitted Hick–Hyman models and Fitts' law measurements are not implemented.

## Separation from gameplay

`TrainingCourseAnalysis.Pages` reads an `AssessmentReport` and returns display strings. `TrainingPresentation.ShowReport` inserts those pages into report navigation. The helper does not change the report, award points, move the player, alter fire or NPC behavior, or modify XR inputs. Navigation retains at most three buttons. Older reports can be read using the same event fields; missing observations remain explicitly missing.

## Recorded verification

On 9 November 2026, the working project compiled runtime, editor and test assemblies and passed the 23 managed assessment-rule checks. Four course-analysis tests passed in Unity EditMode; all four existing controller PlayMode regressions passed; and the focused report-navigation PlayMode test passed after its synthetic event descriptions were corrected. The navigation test checks that the added pages and original timeline remain reachable, the button limit holds, and scores and raw events remain unchanged.

The public repository preserves its existing procedural fire-crackle substitution and sanitized settings. Automated results do not establish physical headset performance or completion of a clean import of this public copy. See [testing](TESTING.md).
