# Scenarios and assessment

## Learning before assessment

Seven narrated lessons cover the represented fire-response topics, equipment and PASS. A four-question quiz gives explanations during practice. Practice is separate from the assessment score; no live correctness hints appear during assessment play.

## Game configuration

Custom games select difficulty, fire kind, intensity, location and floor. Random mode independently samples settings with its own random generator; it is not a cycle through the seven retained instructor presets, and combinations may repeat.

Represented locations include classrooms, offices, computer labs, corridors, the lift lobby, cooler, stair, washrooms and ground-floor exits. Ground-floor exit incidents are restricted to ground level. Intensity controls the starting incident size; difficulty changes timing and development parameters. A small source may remain localized, while developing and large incidents can spread and block routes.

## Fire and equipment model

Fire uses a source and preallocated spread zones. Smoke particles are capped and confined below the floor ceiling. Closing the source-room door before external spread delays its spread; a corridor zone already burning does not disappear when the door is closed. Suppression stops new emission while existing smoke fades.

Portable suppression credit depends on a manageable fire, a compatible agent, a clear escape route, the alarm and observed PASS actions. Water use against electrical or liquid hazards is recorded as unsafe. Water streams respect obstruction. These are explicit training rules, not a fire-dynamics or equipment-certification model. The single extinguisher uses a provisional ABC profile until the real label is verified.

Alarm panels and assistance panels/phones record separate actions. Assistance requests are simulated and do not contact a real emergency service.

## Occupants and routes

Twelve NPCs begin as students in class, teachers teaching and staff working in offices or on corridor duty. They respond after role-specific delays to the alarm or visible fire. Teachers can alert students. Occupants use doors, avoid known fire obstructions and replan routes.

Routing first chooses an available building exit, then continues to assembly. The model has one staircase and two ground-floor exits. A second ground exit does not create a second staircase: upper-floor occupants may remain awaiting assistance when the only stair is blocked. Ordinary offices are not represented as certified refuge areas. NPC-to-NPC collision is disabled to reduce doorway deadlocks; crowd flow is not scientifically validated.

## Analysis

The result records elapsed time, response actions, alarm/assistance timing, movement, exposure, route decisions, equipment/PASS observations, NPC exit outcomes and relevant events. Recommendations use the recorded actions. Ending in a closed office awaiting help is reported as pending assistance, not a completed rescue. A fast but unsafe response is not treated as successful solely because it is fast.
