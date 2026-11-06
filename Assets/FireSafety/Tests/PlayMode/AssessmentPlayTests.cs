using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FireSafety.Tests
{
    public class AssessmentPlayTests
    {
        TrainingSession session;
        float oldScale;
        [UnitySetUp] public IEnumerator OpenAssessmentScene()
        {
            oldScale=Time.timeScale;Time.timeScale=1;
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded("FireSafetyTraining"),"First run Fire Safety > Build Complete Assessment Environment.");
            yield return SceneManager.LoadSceneAsync("FireSafetyTraining");yield return null;yield return null;
            session=Object.FindObjectOfType<TrainingSession>();Assert.IsNotNull(session);
            session.persistLatestReport=false;
            session.player.enabled=false;
        }
        [UnityTearDown] public IEnumerator Cleanup(){Time.timeScale=oldScale;if(session!=null&&session.phase==SessionPhase.Assessing)session.Finish("Automated test cleanup");yield return null;}
        [UnityTest] public IEnumerator StartingScenarioResetsStateAndKeepsHintsHidden()
        {
            session.StartAssessment(0);yield return null;
            Assert.AreEqual(SessionPhase.Assessing,session.phase);Assert.IsFalse(session.presentation.Visible);
            Assert.IsTrue(session.incident.Active);Assert.IsFalse(session.AlarmRaised);Assert.IsFalse(session.PrimaryBlocked);
            Assert.AreEqual(12,session.occupants.Length);
        }
        [UnityTest] public IEnumerator ReportLevelsDesktopViewAndCanBeReopenedFromMenu()
        {
            session.StartAssessment(0);
            session.player.Head.localRotation=Quaternion.Euler(75,0,0);
            session.Finish("Automated report visibility test");yield return null;
            Assert.Less(Mathf.Abs(session.player.Head.forward.y),.001f);
            Assert.Greater(Vector3.Dot(session.player.Head.forward,(session.presentation.panel.position-session.player.Head.position).normalized),.99f);
            Assert.AreEqual(CursorLockMode.None,Cursor.lockState);Assert.IsTrue(Cursor.visible);
            session.ReturnToMenu();session.presentation.buttons[1].Activate(session.player);session.presentation.ShowGameMore();var latest=session.presentation.buttons[0];
            Assert.IsTrue(latest.gameObject.activeSelf);Assert.AreEqual(InteractionAction.LastReport,latest.action);
            latest.Activate(session.player);yield return null;
            Assert.AreEqual(SessionPhase.Complete,session.phase);StringAssert.Contains("SESSION ANALYSIS",session.presentation.heading.text);
            StringAssert.Contains("Automated report visibility test",session.presentation.body.text);
        }
        [UnityTest] public IEnumerator ElevatorAttemptIsRecordedAndDoesNotMoveCabin()
        {
            session.StartAssessment(0);float y=session.lift.cabin.position.y;
            session.lift.RequestFloor(0,true);yield return new WaitForSeconds(.15f);
            Assert.AreEqual(y,session.lift.cabin.position.y,.02f);Assert.AreEqual(1,session.report.unsafeDecisions);
            Assert.IsTrue(session.report.events.Exists(e=>e.action=="Elevator attempted during fire"));
        }
        [UnityTest] public IEnumerator BlockedMainStairsHaveNoInventedAlternativeRoute()
        {
            session.StartAssessment(3);session.RaiseAlarm();session.CallAssistance();
            foreach(var npc in session.occupants)npc.enabled=false;
            int start=session.graph.Nearest(session.player.Feet);
            var route=session.graph.FindPath(start,n=>n.kind==RouteKind.Assembly,true,true);
            Assert.IsEmpty(route);Assert.IsFalse(session.HasSafeEgress);yield return null;
            Assert.AreEqual(SessionPhase.Assessing,session.phase);
        }
        [UnityTest] public IEnumerator MainStairsLeadThroughCorrectEntranceToAssembly()
        {
            session.StartAssessment(0);session.RaiseAlarm();session.CallAssistance();
            foreach(var npc in session.occupants)npc.enabled=false;
            var route=session.graph.FindPath(session.graph.Nearest(session.player.Feet),n=>n.kind==RouteKind.Assembly,false,true);
            Assert.IsNotEmpty(route);yield return Walk(route);
            Assert.AreEqual(SessionPhase.Complete,session.phase);StringAssert.Contains("assembly",session.report.outcome.ToLowerInvariant());
            Assert.IsTrue(session.presentation.Visible);Assert.AreSame(session.report,session.history[session.history.Count-1]);
            Assert.IsNull(session.LastSavedDirectory,"Default report should stay inside the application.");
        }
        [UnityTest] public IEnumerator BriefingWaitsForBeginAndStopsNarrationWhenAssessmentStarts()
        {
            session.PrepareAssessment(1);yield return new WaitForSeconds(.25f);
            Assert.AreEqual(SessionPhase.Briefing,session.phase);Assert.AreEqual(0,session.Elapsed);Assert.IsFalse(session.incident.Active);
            StringAssert.Contains("ENERGIZED ELECTRICAL",session.presentation.body.text);
            Assert.AreSame(session.audioDirector.fireBriefings[1],session.audioDirector.narration.clip);
            session.presentation.buttons[0].Activate(session.player);yield return null;
            Assert.AreEqual(SessionPhase.Assessing,session.phase);Assert.AreEqual(FireKind.EnergizedElectrical,session.incident.kind);
            Assert.IsFalse(session.presentation.Visible);Assert.IsFalse(session.audioDirector.narration.isPlaying);
        }
        [UnityTest] public IEnumerator FullSetBriefingsMatchEveryStartedScenario()
        {
            session.PrepareAssessment(0,true);var seen=new HashSet<string>();
            for(int i=0;i<7;i++)
            {
                int pending=session.PendingScenarioIndex;Assert.AreEqual(SessionPhase.Briefing,session.phase);
                Assert.IsFalse(session.incident.Active,"Previous incident must be cleared before the next briefing.");
                StringAssert.Contains(TrainingInstructions.FireName(session.scenarios[pending].fireKind),session.presentation.body.text);
                session.BeginPreparedAssessment();yield return null;
                Assert.AreEqual(pending,session.scenarioIndex);Assert.IsTrue(seen.Add(session.Scenario.id));
                session.Finish("Automated sequence test");session.Next();
            }
            Assert.AreEqual(7,seen.Count);Assert.AreEqual(SessionPhase.Ready,session.phase);
        }
        [UnityTest] public IEnumerator ManualAlarmAndAssistanceWorkWhileHoldingExtinguisher()
        {
            session.StartAssessment(0);session.player.Grab(session.extinguishers[2]);
            var controls=Object.FindObjectsOfType<TrainingInteractable>();
            System.Array.Find(controls,c=>c.action==InteractionAction.Alarm).Activate(session.player);yield return null;
            Assert.IsTrue(session.AlarmRaised);Assert.Greater(session.alarmAudio.volume,.3f);Assert.IsNotNull(session.alarmAudio.clip);
            Assert.AreEqual("TrainingAlarm",session.alarmAudio.clip.name);Assert.IsTrue(session.alarmAudio.loop);
            Assert.AreSame(session.audioDirector.alarmAcknowledged,session.audioDirector.narration.clip);
            System.Array.Find(controls,c=>c.action==InteractionAction.Assistance).Activate(session.player);yield return null;
            Assert.IsTrue(session.AssistanceRequested);Assert.AreSame(session.audioDirector.callAcknowledged,session.audioDirector.narration.clip);
        }
        [UnityTest] public IEnumerator TheoryAndPracticeLeadBackToTheSelectedAssessment()
        {
            string originalReport=JsonUtility.ToJson(session.report);int originalHistory=session.history.Count;
            session.presentation.ShowMenu();session.presentation.buttons[0].Activate(session.player);
            Assert.AreEqual(SessionPhase.Ready,session.phase);Assert.IsFalse(session.incident.Active);
            for(int i=0;i<TrainingTheory.Course.pages.Length;i++)
            {
                session.presentation.ShowTheory(i);yield return null;
                Assert.AreSame(session.audioDirector.theoryLessons[i],session.audioDirector.narration.clip);
            }
            session.presentation.buttons[1].Activate(session.player);
            for(int i=0;i<TrainingTheory.Course.questions.Length;i++)
            {
                Assert.AreSame(session.audioDirector.theoryQuestions[i],session.audioDirector.narration.clip);
                session.presentation.AnswerTheory(TrainingTheory.Course.questions[i].correctAnswer);
                Assert.AreSame(session.audioDirector.theoryAnswers[i],session.audioDirector.narration.clip);
                session.presentation.NextTheoryQuestion();yield return null;
            }
            StringAssert.Contains("Practice result: 4 / 4",session.presentation.body.text);Assert.AreEqual(0,session.Elapsed);
            Assert.AreEqual(originalReport,JsonUtility.ToJson(session.report));Assert.AreEqual(originalHistory,session.history.Count);
            session.presentation.buttons[0].Activate(session.player);session.PrepareGame(false);
            Assert.AreEqual(SessionPhase.Briefing,session.phase);int chosen=session.PendingScenarioIndex;
            session.presentation.buttons[0].Activate(session.player);yield return null;
            Assert.AreEqual(SessionPhase.Assessing,session.phase);Assert.AreEqual(session.scenarios[chosen].fireKind,session.incident.kind);
            Assert.IsFalse(session.presentation.Visible);Assert.IsFalse(session.audioDirector.narration.isPlaying);
        }
        [UnityTest] public IEnumerator ActiveAssessmentCannotOpenTheoryOrPracticeFeedback()
        {
            session.StartAssessment(0);yield return null;
            float before=session.Elapsed;
            session.presentation.ShowTheory(1);session.presentation.BeginTheoryQuiz();session.presentation.AnswerTheory(1);
            session.presentation.NextTheoryQuestion();session.presentation.ReplayTheory();session.presentation.BackToOrientation();
            session.presentation.ShowAssessmentSelection();yield return new WaitForSeconds(.2f);
            Assert.AreEqual(SessionPhase.Assessing,session.phase);Assert.IsFalse(session.presentation.Visible);
            Assert.Greater(session.Elapsed,before);Assert.IsTrue(session.incident.Active);
        }
        [UnityTest] public IEnumerator RefugeRequiresClosedDoorAndSimulatedCall()
        {
            session.StartAssessment(5);session.RaiseAlarm();
            foreach(var npc in session.occupants)npc.enabled=false;
            var route=session.graph.FindPath(session.graph.Nearest(session.player.Feet),n=>n.kind==RouteKind.Refuge&&n.position.y>6,true,true);
            session.refugeDoors[2].SetForNPC(true);yield return new WaitForSeconds(.8f);
            yield return Walk(route);
            Assert.AreEqual(RouteKind.Refuge,TrainingSession.Locate(session.player.Feet));
            yield return new WaitForSeconds(5.1f);Assert.AreEqual(SessionPhase.Assessing,session.phase,"A refuge alone must not end the assessment.");
            session.CallAssistance();session.refugeDoors[2].SetForNPC(false);
            yield return new WaitForSeconds(5.3f);
            Assert.AreEqual(SessionPhase.Complete,session.phase);StringAssert.Contains("assistance",session.report.outcome.ToLowerInvariant());
        }
        [UnityTest] public IEnumerator CorrectPassTechniqueCanSuppressIncident()
        {
            // Equipment technique is tested independently of a moving occupant crossing the nozzle.
            session.StartAssessment(1);session.RaiseAlarm();yield return new WaitForSeconds(1);
            foreach(var npc in session.occupants){npc.enabled=false;npc.GetComponent<CharacterController>().enabled=false;}
            var tool=session.extinguishers[2];session.player.Grab(tool);tool.PullPin();
            Vector3 target=session.incident.transform.position+Vector3.up*.18f;
            Vector3 source=target+new Vector3(-3,1,0);
            for(int i=0;i<6;i++){tool.Tick(false,new Ray(source,(target-source).normalized),.1f);yield return null;}
            for(int i=0;i<180&&!session.incident.Extinguished;i++)
            {
                Vector3 offset=Vector3.forward*(i%20<10?-.25f:.25f);
                tool.Tick(true,new Ray(source,(target+offset-source).normalized),.1f);yield return null;
            }
            Assert.IsTrue(session.report.pinPulled);Assert.IsTrue(session.report.aimedAtBase);Assert.IsTrue(session.report.squeezed);Assert.IsTrue(session.report.swept);
            Assert.IsTrue(session.incident.Extinguished,"Check that doorway and aim line are unobstructed.");
            Assert.IsTrue(session.report.passInOrder);
            int pull=session.report.events.FindIndex(e=>e.action=="PASS: pull");int aim=session.report.events.FindIndex(e=>e.action=="PASS: aim");
            int squeeze=session.report.events.FindIndex(e=>e.action=="PASS: squeeze");int sweep=session.report.events.FindIndex(e=>e.action=="PASS: sweep");
            Assert.IsTrue(pull<aim&&aim<squeeze&&squeeze<sweep);
        }
        [UnityTest] public IEnumerator SqueezingBeforePinIsRecordedAsSequenceError()
        {
            session.StartAssessment(1);var tool=session.extinguishers[2];session.player.Grab(tool);
            tool.Tick(true,new Ray(session.player.Head.position,Vector3.right),.1f);yield return null;
            Assert.IsFalse(tool.PinRemoved);Assert.AreEqual(22,tool.remaining,.001f);Assert.IsFalse(session.report.passInOrder);
            Assert.IsTrue(session.report.events.Exists(e=>e.action=="PASS sequence error"));
        }
        [UnityTest] public IEnumerator AlarmMakesOccupantsAvoidBlockedMainStair()
        {
            session.StartAssessment(3);session.RaiseAlarm();
            var initial=new Vector3[session.occupants.Length];for(int i=0;i<initial.Length;i++)initial[i]=session.occupants[i].transform.position;
            yield return new WaitForSeconds(12);
            int moved=0;for(int i=0;i<initial.Length;i++)
            {
                var n=session.occupants[i];if(Vector3.Distance(initial[i],n.transform.position)>.5f)moved++;
                Assert.AreNotEqual(RouteKind.PrimaryStairs,TrainingSession.Locate(n.transform.position));
            }
            Assert.GreaterOrEqual(moved,10);
        }
        [UnityTest] public IEnumerator DevelopingScenarioChangesRouteAndRecordsIt()
        {
            session.scenarios[6].escalationSeconds=.2f;session.StartAssessment(6);yield return new WaitForSeconds(.4f);
            Assert.IsTrue(session.PrimaryBlocked);Assert.IsTrue(session.report.events.Exists(e=>e.action=="Route conditions changed"));
        }
        [UnityTest] public IEnumerator DevelopingFireSpreadsIntoCorridorAnotherRoomAndStairApproach()
        {
            session.scenarios[6].escalationSeconds=.3f;session.StartAssessment(6);yield return new WaitForSeconds(.5f);
            Assert.IsTrue(session.PrimaryBlocked);Assert.AreEqual(3,session.report.spreadZonesActivated);
            Assert.IsTrue(System.Array.TrueForAll(session.fireDevelopment.zones,z=>z.Active));
            Assert.Greater(Vector3.Distance(session.incident.transform.position,session.fireDevelopment.zones[2].transform.position),5);
            Assert.IsTrue(session.report.events.Exists(e=>e.action=="Fire spread"&&e.detail.Contains("another")));
            session.incident.Grow(40);Assert.Greater(session.incident.footprintScale,1.45f);Assert.IsFalse(session.incident.PortableSuppressionSuitable);
        }
        [UnityTest] public IEnumerator ClosingSourceDoorDelaysExternalSpread()
        {
            session.scenarios[6].escalationSeconds=.2f;session.StartAssessment(6);
            session.fireDevelopment.sourceDoor.SetForNPC(false);yield return new WaitForSeconds(.4f);
            Assert.IsTrue(session.fireDevelopment.zones[0].Active);Assert.IsFalse(session.fireDevelopment.zones[1].Active);
            Assert.IsFalse(session.PrimaryBlocked);Assert.AreEqual(0,session.fireDevelopment.SpreadExposure,.02f);
            session.fireDevelopment.sourceDoor.SetForNPC(true);yield return new WaitForSeconds(.35f);
            Assert.IsTrue(session.fireDevelopment.zones[1].Active);Assert.IsTrue(session.PrimaryBlocked);
        }
        [UnityTest] public IEnumerator EarlySuppressionPreventsSpreadAndDangerousFireCannotBeInstantlyCleared()
        {
            session.scenarios[6].escalationSeconds=.3f;session.StartAssessment(6);session.incident.Suppress(8);yield return new WaitForSeconds(.5f);
            Assert.IsTrue(session.incident.Extinguished);Assert.AreEqual(0,session.report.spreadZonesActivated);Assert.IsFalse(session.PrimaryBlocked);
            session.Finish("Automated reset");session.StartAssessment(3);yield return null;
            Assert.IsFalse(session.incident.PortableSuppressionSuitable);float strength=session.incident.strength;session.incident.Suppress(100);
            Assert.IsTrue(session.incident.Active);Assert.AreEqual(strength,session.incident.strength);
        }
        [UnityTest] public IEnumerator EntranceFireLeavesTheSecondGroundExitAvailable()
        {
            session.StartAssessment(4);yield return null;
            Assert.IsTrue(session.ExitBlocked);Assert.IsTrue(session.HasSafeEgress);Assert.AreEqual(0,session.CurrentFloor);
            Assert.Greater(session.incident.HeatAt(new Vector3(-4.2f,0,11.3f)),0);
            Assert.Greater(session.incident.HeatAt(new Vector3(-4.2f,0,14.7f)),0);
            int start=session.graph.Nearest(session.player.Feet);
            var alternative=session.graph.FindPath(start,n=>n.kind==RouteKind.Assembly,false,true,true);
            Assert.IsNotEmpty(alternative);Assert.IsTrue(alternative.Exists(i=>session.graph.nodes[i].secondaryExit));
            Assert.IsNotEmpty(session.graph.FindPath(start,n=>n.kind==RouteKind.Refuge&&n.position.y<1,false,true,true));
        }
        [UnityTest] public IEnumerator AllTwelveOccupantsCanReachAssemblyViaMainStairs()
        {
            session.StartAssessment(0);session.RaiseAlarm();yield return new WaitForSeconds(90);
            foreach(var npc in session.occupants)Assert.IsTrue(npc.AtAssembly,npc.name+" stopped at "+npc.transform.position+" ("+npc.activity+")");
            for(int i=0;i<3;i++)Assert.AreEqual("Second exit beside lift",session.occupants[i].ExitUsed,"Ground classroom should choose nearby exit");
            Assert.AreEqual("Main entrance",session.occupants[3].ExitUsed,"Ground corridor staff is nearer the main entrance");
        }
        [UnityTest] public IEnumerator StudentsAndStaffStayAtAssignedWorkplacesUntilAlerted()
        {
            session.gameSettings=new TrainingGameSettings {location=2,floor=2,intensity=0};session.PrepareGame(false);session.BeginPreparedAssessment();
            var positions=new Vector3[session.occupants.Length];for(int i=0;i<positions.Length;i++)positions[i]=session.occupants[i].transform.position;
            yield return new WaitForSeconds(8);
            for(int i=0;i<positions.Length;i++){Assert.IsFalse(session.occupants[i].Emergency);Assert.Less(Vector3.Distance(positions[i],session.occupants[i].transform.position),.02f);}
            session.RaiseAlarm();yield return new WaitForSeconds(6);
            foreach(var npc in session.occupants)Assert.IsTrue(npc.Emergency,npc.RoleLabel);
        }
        [UnityTest] public IEnumerator CustomGameStartsEveryLocationAtSelectedIntensityAndFloor()
        {
            for(int site=0;site<IncidentSites.Names.Length;site++)for(int size=0;size<3;size++)
            {
                session.gameSettings=new TrainingGameSettings {location=site,floor=1,intensity=size,difficulty=2,fireKind=FireKind.EnergizedElectrical};
                session.PrepareGame(false);session.BeginPreparedAssessment();
                Assert.AreEqual(IncidentSites.Position(site,IncidentSites.GroundOnly(site)?0:1),session.incident.transform.position);
                Assert.AreEqual(size<2,session.incident.PortableSuppressionSuitable);Assert.AreEqual(site==6,session.PrimaryBlocked);Assert.AreEqual(site==7,session.ExitBlocked);
                Assert.Greater(Vector3.Distance(session.player.Feet,session.incident.transform.position),2,"Observer must start outside the source footprint");
                session.Finish("Automated custom setup");yield return null;
            }
        }
        [UnityTest] public IEnumerator BlockedMainStairSendsUpperOccupantsToRoomsAndGroundOccupantsOutside()
        {
            session.StartAssessment(3);session.RaiseAlarm();yield return new WaitForSeconds(70);
            int assembly=0,waiting=0;
            foreach(var npc in session.occupants){if(npc.AtAssembly)assembly++;if(npc.AtRefuge)waiting++;Assert.IsTrue(npc.AtAssembly||npc.AtRefuge,npc.name+" stuck at "+npc.transform.position);}
            Assert.AreEqual(4,assembly);Assert.AreEqual(8,waiting);
        }
        [UnityTest] public IEnumerator EveryMainAndHalfFloorRoomCanBeEntered()
        {
            session.StartAssessment(0);foreach(var npc in session.occupants)npc.enabled=false;
            foreach(var door in session.doors)
            {
                door.SetForNPC(true);yield return new WaitForSeconds(.75f);
                Vector3 start,end;Vector3 p=door.transform.position;
                if(p.z>=30)
                {start=new Vector3(0,p.y+.04f,p.z-1);end=new Vector3(0,p.y,p.z+1.2f);}
                else if(Mathf.Abs(p.x)<2)
                {
                    float side=Mathf.Sign(p.x);start=new Vector3(0,p.y+.04f,p.z+.56f);end=new Vector3(side*3,p.y,p.z+.56f);
                }
                else
                {
                    bool north=p.z<13;float side=north?-1:1;
                    start=new Vector3(p.x+.56f,p.y+.04f,p.z-side*1.05f);end=new Vector3(p.x+.56f,p.y,p.z+side*1.0f);
                }
                session.player.Teleport(start,Quaternion.identity);
                int node=session.graph.Add("Door crossing "+door.roomName,end);yield return Walk(new List<int>{node});
                Assert.Less(Vector3.Distance(session.player.Feet,end),.6f,"Could not enter "+door.roomName);
            }
        }
        [UnityTest] public IEnumerator WashroomVisitDoesNotCreateABlockedStairPenalty()
        {
            session.StartAssessment(0);session.player.Teleport(new Vector3(3,7.04f,27),Quaternion.identity);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(RouteKind.Washroom,session.CurrentRoute);Assert.AreEqual(0,session.report.unsafeDecisions);
            Assert.IsFalse(session.report.events.Exists(e=>e.action=="Blocked route entry"));
        }
        [UnityTest] public IEnumerator BucketFillsFromTapAndStopsAtCapacity()
        {
            session.StartAssessment(0);yield return null;
            var bucket=TopFloorBucket();var source=TopFloorWashroomTap();
            session.player.Teleport(new Vector3(5.2f,7.04f,26),Quaternion.identity);
            session.player.GrabBucket(bucket);source.Toggle();bucket.FillFrom(source,10);
            Assert.AreEqual(4,bucket.waterLiters,.01f);Assert.AreEqual(4,session.report.waterCollectedLiters,.01f);
            Assert.IsTrue(bucket.waterSurface.gameObject.activeSelf);
            Assert.AreEqual(0,bucket.FillFrom(source,1),.001f);
            source.Toggle();Assert.IsFalse(source.Flowing);
        }
        [UnityTest] public IEnumerator ActiveFireHasVisibleSmokeConfinedBelowThisFloorsCeiling()
        {
            session.StartAssessment(0);yield return null;
            var fire=session.incident;var smoke=fire.smoke;
            Assert.IsTrue(smoke.isEmitting);Assert.Greater(smoke.particleCount,0,"An already burning incident should show smoke immediately");
            smoke.Simulate(5,true,false,true);fire.ConfineSmokeToFloor();
            var particles=new ParticleSystem.Particle[80];int count=smoke.GetParticles(particles);
            Assert.Greater(count,20);Assert.LessOrEqual(count,80);
            float ceiling=7+3.25f;
            for(int i=0;i<count;i++)Assert.LessOrEqual(particles[i].position.y+particles[i].GetCurrentSize(smoke)*.71f,ceiling+.01f);
            var gradient=smoke.colorOverLifetime.color.gradient;float opacity=smoke.main.startColor.color.a*gradient.Evaluate(.15f).a;
            Assert.Greater(opacity,.6f,"Smoke must not return to the old near-transparent alpha combination");
            fire.ResetHazard(fire.kind,false);Assert.AreEqual(0,smoke.particleCount);
        }
        [UnityTest] public IEnumerator SmokeThickensWithGrowthAndStopsWhenSourceIsSuppressed()
        {
            session.StartAssessment(0);yield return null;var fire=session.incident;
            float smallRate=fire.smoke.emission.rateOverTime.constant,smallSize=fire.smoke.main.startSize.constant;
            fire.Grow(100);Assert.Greater(fire.smoke.emission.rateOverTime.constant,smallRate);Assert.Greater(fire.smoke.main.startSize.constant,smallSize);
            Assert.LessOrEqual(fire.smoke.main.maxParticles,80);
            fire.ResetHazard(FireKind.OrdinaryCombustibles,true);yield return null;
            fire.Suppress(8);Assert.IsFalse(fire.Active);Assert.IsFalse(fire.smoke.isEmitting);
            session.ReturnToMenu();Assert.AreEqual(0,fire.smoke.particleCount);
        }
        [UnityTest] public IEnumerator SwitchingIncidentLocationStartsSmokeAtTheNewSource()
        {
            session.StartAssessment(0);yield return null;session.Finish("Smoke location reset fixture");
            session.gameSettings=new TrainingGameSettings {location=2,floor=0,intensity=0};session.PrepareGame(false);session.BeginPreparedAssessment();
            var particles=new ParticleSystem.Particle[80];int count=session.incident.smoke.GetParticles(particles);Assert.Greater(count,0);
            foreach(var particle in particles)
            {
                if(particle.remainingLifetime<=0)continue;
                Assert.Less(Vector2.Distance(new Vector2(particle.position.x,particle.position.z),new Vector2(session.incident.transform.position.x,session.incident.transform.position.z)),1,"Initial smoke must not remain at the previous room/floor");
                Assert.Less(particle.position.y,3.25f);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator WaterBucketCannotSuppressEnergizedElectricalFire()
        {
            session.StartAssessment(1);session.RaiseAlarm();yield return new WaitForSeconds(.8f);
            ClearOccupantsForEquipmentTest();
            var bucket=FillTopFloorBucket();var target=session.incident.transform.position+Vector3.up*.18f;
            var source=target+new Vector3(-3,1,0);var ray=new Ray(source,(target-source).normalized);
            AssertPourLineClear(ray,target);
            float strength=session.incident.strength;
            bucket.Tick(true,ray,.2f);bucket.Tick(true,ray,.2f);yield return null;
            Assert.AreEqual(strength,session.incident.strength,.001f);Assert.AreEqual(0,session.report.waterEffectiveLiters,.001f);
            Assert.AreEqual(1,session.report.unsafeWaterUses);Assert.IsTrue(session.report.events.Exists(e=>e.action=="Unsafe water use"));
        }
        [UnityTest] public IEnumerator FilledBucketCanSuppressSmallOrdinaryFireWithoutCreditingPass()
        {
            session.StartAssessment(0);session.RaiseAlarm();yield return new WaitForSeconds(.8f);
            ClearOccupantsForEquipmentTest();
            var bucket=FillTopFloorBucket();var target=session.incident.transform.position+Vector3.up*.18f;
            var origin=target+new Vector3(-3,1,0);var ray=new Ray(origin,(target-origin).normalized);
            AssertPourLineClear(ray,target);
            for(int i=0;i<10&&!session.incident.Extinguished;i++){bucket.Tick(true,ray,.4f);yield return null;}
            Assert.IsTrue(session.incident.Extinguished);Assert.Greater(session.report.waterEffectiveLiters,3);
            Assert.IsFalse(session.report.extinguisherAttempted);Assert.IsFalse(session.report.pinPulled);
        }
        [UnityTest] public IEnumerator WaterOnLargeElectricalFireStillRecordsUnsuitableAgent()
        {
            session.gameSettings=new TrainingGameSettings {fireKind=FireKind.EnergizedElectrical,intensity=2,location=0,floor=2};
            session.PrepareGame(false);session.BeginPreparedAssessment();session.RaiseAlarm();yield return null;
            ClearOccupantsForEquipmentTest();var bucket=FillTopFloorBucket();
            var target=session.incident.transform.position+Vector3.up*.18f;var origin=target+new Vector3(-3,1,0);var ray=new Ray(origin,(target-origin).normalized);
            AssertPourLineClear(ray,target);float strength=session.incident.strength;
            bucket.Tick(true,ray,.2f);bucket.Tick(true,ray,.2f);
            Assert.AreEqual(1,session.report.unsafeWaterUses);Assert.AreEqual(0,session.report.waterEffectiveLiters);Assert.AreEqual(strength,session.incident.strength);Assert.IsTrue(session.incident.Active);
            Assert.IsTrue(session.report.events.Exists(e=>e.action=="Unsafe water use"));
        }
        [UnityTest] public IEnumerator ABlockedWaterStreamConsumesWaterWithoutSuppressingTheFire()
        {
            session.StartAssessment(0);session.RaiseAlarm();yield return null;ClearOccupantsForEquipmentTest();
            var bucket=FillTopFloorBucket();var target=session.incident.transform.position+Vector3.up*.18f;var origin=target+new Vector3(-3,1,0);
            var ray=new Ray(origin,(target-origin).normalized);AssertPourLineClear(ray,target);
            var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.name="Water obstruction test";
            obstruction.transform.position=Vector3.Lerp(origin,target,.5f);obstruction.transform.localScale=Vector3.one*.65f;Physics.SyncTransforms();
            try
            {
                float strength=session.incident.strength;bucket.Tick(true,ray,.4f);
                Assert.Less(bucket.waterLiters,4);Assert.AreEqual(0,session.report.waterEffectiveLiters);Assert.AreEqual(strength,session.incident.strength);
                obstruction.GetComponent<Collider>().enabled=false;Physics.SyncTransforms();bucket.Tick(true,ray,.4f);
                Assert.Greater(session.report.waterEffectiveLiters,0);Assert.Less(session.incident.strength,strength);
            }
            finally {Object.Destroy(obstruction);}
        }
        void ClearOccupantsForEquipmentTest()
        {
            // Crowd motion has its own tests. Isolate agent/aim rules from bodies crossing the stream.
            foreach(var npc in session.occupants){npc.enabled=false;npc.GetComponent<CharacterController>().enabled=false;}
            Physics.SyncTransforms();
        }
        TrainingWaterSource TopFloorWashroomTap()
        {
            foreach(var source in session.waterSources)if(source.sourceName=="Male washroom, floor 2 tap")return source;
            Assert.Fail("Top-floor washroom source missing");return null;
        }
        void AssertPourLineClear(Ray ray,Vector3 target)
        {
            bool blocked=Physics.Raycast(ray,out RaycastHit hit,Vector3.Distance(ray.origin,target)-.5f,~(1<<5),QueryTriggerInteraction.Ignore);
            Assert.IsFalse(blocked,"Pour line blocked by "+(blocked?hit.collider.name+" / "+hit.collider.transform.root.name:"nothing"));
        }
        TrainingBucket FillTopFloorBucket()
        {
            var bucket=TopFloorBucket();var source=TopFloorWashroomTap();
            session.player.Teleport(new Vector3(5.2f,7.04f,26),Quaternion.identity);session.player.GrabBucket(bucket);
            source.Toggle();bucket.FillFrom(source,10);source.Toggle();return bucket;
        }
        TrainingBucket TopFloorBucket()
        {
            foreach(var bucket in session.buckets)if(bucket.locationName=="Male washroom, floor 2")return bucket;
            Assert.Fail("Top-floor male washroom bucket missing");return null;
        }
        IEnumerator Walk(List<int> route)
        {
            var body=session.player.Body;float vertical=0;
            foreach(int index in route)
            {
                Vector3 target=session.graph.nodes[index].position;int frames=0;
                while(session.phase==SessionPhase.Assessing)
                {
                    var current=session.player.Feet;Vector3 delta=Vector3.ProjectOnPlane(target-current,Vector3.up);
                    if(delta.magnitude<.2f&&Mathf.Abs(target.y-current.y)<.55f)break;
                    Assert.Less(frames++,1600,"Stuck at "+session.graph.nodes[index].id+" from "+current);
                    vertical=body.isGrounded?-1.5f:vertical-9.81f*Time.deltaTime;
                    Vector3 move=Vector3.ClampMagnitude(delta,3.8f*Time.deltaTime);
                    body.Move(move+Vector3.up*vertical*Time.deltaTime);yield return null;
                }
                if(session.phase!=SessionPhase.Assessing)yield break;
            }
            yield return null;
        }
    }
}
