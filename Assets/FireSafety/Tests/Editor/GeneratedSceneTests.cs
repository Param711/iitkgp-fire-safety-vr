using System.Linq;
using FireSafety.Editor;
using NUnit.Framework;
using UnityEngine;

namespace FireSafety.Tests
{
    public class GeneratedSceneTests
    {
        TrainingSession session;
        [OneTimeSetUp] public void BuildScene(){TrainingSceneBuilder.Build();session=Object.FindObjectOfType<TrainingSession>();}
        [Test] public void HasTwelveConfiguredOccupants()
        {Assert.AreEqual(12,session.occupants.Length);Assert.IsTrue(session.occupants.All(n=>n.graph==session.graph&&n.session==session));}
        [Test] public void EveryControlHasASessionReference()
        {Assert.IsTrue(Object.FindObjectsOfType<TrainingInteractable>(true).All(i=>i.session==session));}
        [Test] public void EveryRoomHasAnInteractiveDoor()
        {Assert.AreEqual(28,session.doors.Length);Assert.IsTrue(session.doors.All(d=>d.leaf!=null));}
        [Test] public void AllToolsAndRefugesAreAvailableOnEachFloor()
        {Assert.AreEqual(3,session.extinguishers.Length);Assert.AreEqual(1,session.extinguishers.Select(e=>e.agent).Distinct().Count());Assert.AreEqual(3,session.refugeDoors.Length);Assert.IsTrue(session.refugeDoors.All(d=>d.refugeDoor));}
        [Test] public void OnlyMainStairsConnectTopFloorToAssembly()
        {
            int start=session.graph.Nearest(session.spawnPoints[2].position);
            var a=session.graph.FindPath(start,n=>n.kind==RouteKind.Assembly,false,true);
            var b=session.graph.FindPath(start,n=>n.kind==RouteKind.Assembly,true,false);
            Assert.IsNotEmpty(a);Assert.IsEmpty(b);
            Assert.IsTrue(a.Any(i=>session.graph.nodes[i].kind==RouteKind.PrimaryStairs));
            Assert.IsFalse(a.Any(i=>session.graph.nodes[i].kind==RouteKind.SecondaryStairs));
            Assert.IsFalse(session.graph.nodes.Any(n=>n.kind==RouteKind.SecondaryStairs));
        }
        [Test] public void BothBlockedLeavesOnlyRefugeOnUpperFloor()
        {
            int start=session.graph.Nearest(session.spawnPoints[2].position);
            Assert.IsEmpty(session.graph.FindPath(start,n=>n.kind==RouteKind.Assembly,true,true));
            var safe=session.graph.FindPath(start,n=>n.kind==RouteKind.Refuge&&n.position.y>6,true,true);Assert.IsNotEmpty(safe);
        }
        [Test] public void NoMissingMonoBehavioursOrShaderReferences()
        {
            foreach(var t in Object.FindObjectsOfType<Transform>(true))
                foreach(var component in t.GetComponents<Component>())Assert.IsNotNull(component,"Missing component on "+t.name);
            foreach(var r in Object.FindObjectsOfType<Renderer>(true))foreach(var material in r.sharedMaterials)
            {
                Assert.IsNotNull(material,"Missing material on "+r.name);Assert.IsNotNull(material.shader,"Missing shader on "+r.name);
                Assert.IsFalse(UnityEditor.ShaderUtil.ShaderHasError(material.shader),"Shader compile error: "+material.shader.name);
            }
        }
        [Test] public void WorldLabelsUseDepthTestedTextShader()
        {
            foreach(var text in Object.FindObjectsOfType<TextMesh>(true))
            {Assert.IsNotNull(text.GetComponent<TrainingWorldText>());Assert.AreEqual("FireSafety/WorldText",text.GetComponent<MeshRenderer>().sharedMaterial.shader.name);}
        }
        [Test] public void WashroomsBucketsAndCoolersAreWired()
        {
            Assert.AreEqual(6,session.buckets.Length);Assert.AreEqual(9,session.waterSources.Length);
            Assert.AreEqual(6,session.doors.Count(d=>d.roomName.Contains("WASHROOM")));
            foreach(var source in session.waterSources){Assert.IsNotNull(source.spout);Assert.AreSame(session,source.session);}
            foreach(var source in session.waterSources.Where(s=>s.sourceName.StartsWith("Water cooler")))
            {Assert.Greater(source.transform.position.x,0);Assert.AreEqual(-2,source.transform.position.z,.02f);}
        }
        [Test] public void MainEntranceFacesPrimaryStairsAndLeadsToAssembly()
        {
            var entrance=session.graph.nodes.First(n=>n.id=="Main entrance opposite Stair A");
            var stair=session.graph.nodes.First(n=>n.id=="PrimaryStairs entrance 0");
            Assert.AreEqual(stair.position.z,entrance.position.z,.01f);Assert.Less(entrance.position.x,0);Assert.Greater(stair.position.x,0);
            Assert.IsNotEmpty(session.graph.FindPath(session.graph.Nearest(entrance.position),n=>n.kind==RouteKind.Assembly,false,false));
        }
        [Test] public void MainEntranceHasFourMetresOfClearWidth()
        {
            var passage=Object.FindObjectsOfType<Transform>().Single(t=>t.name=="Entrance passage floor");
            Assert.AreEqual(4,passage.localScale.z,.01f);Physics.SyncTransforms();
            int architecture=~((1<<5)|(1<<8)|(1<<9));
            foreach(float z in new[]{11.3f,13f,14.7f})
                Assert.IsFalse(Physics.Raycast(new Vector3(0,1,z),Vector3.left,8,architecture),"Entrance blocked at z="+z);
        }
        [Test] public void BriefingAndBothControlGuidesAreAccessibleBeforeBeginning()
        {
            session.PrepareAssessment(1);
            Assert.AreEqual(SessionPhase.Briefing,session.phase);Assert.IsFalse(session.incident.Active);
            StringAssert.Contains("ENERGIZED ELECTRICAL",session.presentation.body.text);
            Assert.AreEqual(InteractionAction.BeginBriefing,session.presentation.buttons[0].action);
            session.presentation.ShowControls(false);StringAssert.Contains("W A S D",session.presentation.body.text);
            session.presentation.ShowControls(true);StringAssert.Contains("Right grip",session.presentation.body.text);
            session.presentation.ShowEquipmentGuide();StringAssert.Contains("FIRE ALARM",session.presentation.body.text);
            session.ReturnToMenu();
        }
        [Test] public void NarrationOccupantFireAndNozzleAudioAreWired()
        {
            var audio=session.audioDirector;Assert.IsNotNull(audio);Assert.AreSame(session,audio.session);
            Assert.AreEqual(3,audio.fireBriefings.Length);
            foreach(var clip in audio.fireBriefings)Assert.Greater(clip.length,1);
            Assert.Greater(audio.desktopControls.length,1);Assert.Greater(audio.vrControls.length,1);Assert.Greater(audio.equipment.length,1);
            Assert.IsNotNull(audio.soundCheck);Assert.IsNotNull(audio.alarmAcknowledged);Assert.IsNotNull(audio.callAcknowledged);
            Assert.IsTrue(session.occupants.All(n=>n.voice!=null));Assert.IsTrue(session.extinguishers.All(e=>e.sprayAudio.clip!=null));
            Assert.IsTrue(session.routeFires.All(f=>f.fireAudio.clip!=null));Assert.IsNotNull(session.incident.fireAudio.clip);
            Assert.AreEqual(1,Object.FindObjectsOfType<AudioListener>().Length);
            Assert.IsNotNull(session.alarmAudio.clip);Assert.AreEqual("TrainingAlarm",session.alarmAudio.clip.name);Assert.Greater(session.alarmAudio.clip.length,1);
        }
        [Test] public void ResultIsReadableInsideApplicationWithoutHtmlButton()
        {
            session.report=new AssessmentReport{outcome="Review fixture"};session.report.Evaluate(true,false,true,true);session.presentation.ShowReport(0);
            Assert.IsFalse(session.exportReports);Assert.IsFalse(session.presentation.buttons.Any(b=>b.gameObject.activeSelf&&b.action==InteractionAction.OpenReport));
            StringAssert.Contains("Review your result here",session.presentation.footer.text);
        }
        [Test] public void TrackingRecoveryNoticeIsBoundToStereoInterface()
        {
            Assert.IsNotNull(session.player.trackingNotice);Assert.IsNotNull(session.player.trackingNoticeText);
            Assert.AreEqual(5,session.player.trackingNotice.layer);Assert.AreEqual(2,session.player.Head.GetComponentsInChildren<Camera>(true).Length);
        }
        [Test] public void WashroomBranchHasFemaleAheadAndMaleToTheRight()
        {
            var female=session.doors.Where(d=>d.roomName.StartsWith("FEMALE WASHROOM")).ToArray();
            var male=session.doors.Where(d=>d.roomName.StartsWith("MALE WASHROOM")).ToArray();
            Assert.AreEqual(3,female.Length);Assert.AreEqual(3,male.Length);
            foreach(var door in female){Assert.AreEqual(30,door.transform.position.z,.01f);Assert.Less(Mathf.Abs(door.transform.position.x),1);}
            foreach(var door in male){Assert.Greater(door.transform.position.x,1);Assert.Less(door.transform.position.z,30);}
        }
        [Test] public void LongReportKeepsReviewAndTimelinePagesReadable()
        {
            var r=new AssessmentReport{outcome="Review fixture",extinguisherAttempted=true,unsafeDecisions=3,smokeExposureSeconds=20};r.Evaluate(false,false,false,false);
            for(int i=0;i<24;i++)r.events.Add(new AssessmentEvent{action="Decision recorded",detail="Main stair unavailable; participant returned to the corridor, closed the office door and requested assistance.",severity="observation"});
            session.report=r;int pages=0;bool timelineSeen=false;
            session.presentation.ShowReport(2);
            while(pages++<20)
            {
                Assert.GreaterOrEqual(session.presentation.body.characterSize,.013f,"Report shrank below readable page size");
                timelineSeen|=session.presentation.body.text.Contains("OBSERVED DECISIONS");
                var next=session.presentation.buttons[1];if(!next.gameObject.activeSelf)break;
                session.presentation.ShowReport(next.value);
            }
            Assert.IsTrue(timelineSeen);Assert.Less(pages,20);
        }
        [Test] public void RecommendationsRespondToRecordedActionsAndHaveTheirOwnSection()
        {
            var report=new AssessmentReport{outcome="Review fixture",unsafeWaterUses=1,spreadZonesActivated=2,externalSpreadDelaySeconds=5,fireWasInitiallyDangerous=true,extinguisherAttempted=true};
            report.events.Add(new AssessmentEvent{action="Elevator attempted during fire"});report.Evaluate(false,false,false,false);
            Assert.IsTrue(report.recommendations.Any(s=>s.Contains("lift")));Assert.IsTrue(report.recommendations.Any(s=>s.Contains("Do not pour water")));
            Assert.IsTrue(report.recommendations.Any(s=>s.Contains("portable-equipment")));Assert.IsTrue(report.recommendations.Any(s=>s.Contains("Door closure")));
            session.report=report;session.presentation.ShowReport(2);StringAssert.Contains("EQUIPMENT AND FIRE DEVELOPMENT",session.presentation.body.text);
            StringAssert.Contains("PASS order: Incomplete",session.presentation.body.text);
            session.presentation.ShowReport(3);StringAssert.Contains("RECOMMENDATIONS",session.presentation.body.text);
            Assert.LessOrEqual(session.presentation.buttons.Count(b=>b.gameObject.activeSelf),3);
            var safe=new AssessmentReport();safe.Evaluate(true,false,true,true);
            Assert.IsFalse(safe.recommendations.Any(s=>s.Contains("Do not pour water")));
        }
    }
}
