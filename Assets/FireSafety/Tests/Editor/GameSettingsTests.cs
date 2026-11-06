using System.Collections.Generic;
using System.Linq;
using FireSafety.Editor;
using NUnit.Framework;
using UnityEngine;

namespace FireSafety.Tests
{
    public class GameSettingsTests
    {
        [Test] public void RandomSamplesSettingsBeyondTheSevenPresets()
        {
            var random=new System.Random(7319);var combinations=new HashSet<string>();
            var sites=new HashSet<int>();var levels=new HashSet<int>();var sizes=new HashSet<int>();var kinds=new HashSet<FireKind>();var floors=new HashSet<int>();
            for(int i=0;i<1000;i++)
            {
                var s=TrainingGameSettings.Draw(random);var scenario=s.CreateScenario("random");
                sites.Add(s.location);levels.Add(s.difficulty);sizes.Add(s.intensity);kinds.Add(s.fireKind);floors.Add(scenario.startFloor);
                combinations.Add(scenario.title+scenario.fireKind);
                Assert.That(scenario.startFloor,Is.InRange(0,2));if(IncidentSites.GroundOnly(s.location))Assert.AreEqual(0,scenario.startFloor);
            }
            Assert.Greater(combinations.Count,300);Assert.AreEqual(11,sites.Count);Assert.AreEqual(3,levels.Count);Assert.AreEqual(3,sizes.Count);Assert.AreEqual(3,kinds.Count);Assert.AreEqual(3,floors.Count);
        }
        [Test] public void DifficultyAndIntensityHaveIndependentEffects()
        {
            var s=new TrainingGameSettings {difficulty=0,intensity=1};var easy=s.CreateScenario("easy");
            s.difficulty=2;var hard=s.CreateScenario("hard");
            Assert.Less(hard.escalationSeconds,easy.escalationSeconds);Assert.Less(hard.durationLimit,easy.durationLimit);Assert.AreEqual(easy.initialFootprint,hard.initialFootprint);
            s.intensity=0;var small=s.CreateScenario("small");s.intensity=2;var large=s.CreateScenario("large");
            Assert.IsFalse(small.developingFire);Assert.IsTrue(large.developingFire);Assert.Less(small.initialStrength,large.initialStrength);Assert.Greater(large.initialFootprint,1.45f);
        }
        [Test] public void GameWizardPreservesSelectionsAndHasAtMostThreeButtons()
        {
            TrainingSceneBuilder.Build();var session=Object.FindObjectOfType<TrainingSession>();session.persistLatestReport=false;
            session.presentation.ShowMenu();Assert.AreEqual("PLAY",session.presentation.buttonLabels[1].text);
            session.presentation.ShowAssessmentSelection();Assert.AreEqual(InteractionAction.GameRandom,session.presentation.buttons[1].action);
            for(int i=0;i<6;i++){session.presentation.ShowGameSetup(i);Assert.LessOrEqual(session.presentation.buttons.Count(b=>b.gameObject.activeSelf),3);}
            session.gameSettings.location=7;session.gameSettings.floor=2;session.gameSettings.intensity=2;session.PrepareGame(false);
            Assert.AreEqual(SessionPhase.Briefing,session.phase);Assert.AreEqual(0,session.scenarios[session.PendingScenarioIndex].startFloor);
            StringAssert.Contains("LARGE",session.presentation.body.text);Assert.AreEqual(0,session.Elapsed);Assert.IsFalse(session.incident.Active);
            for(int i=0;i<10;i++)session.PrepareGame(true);Assert.AreEqual(8,session.scenarios.Length);
            session.ReturnToMenu();
        }
        [Test] public void OccupantsHaveClassroomAndWorkplaceRoles()
        {
            TrainingSceneBuilder.Build();var session=Object.FindObjectOfType<TrainingSession>();
            Assert.AreEqual(6,session.occupants.Count(n=>n.role==OccupantRole.Student));Assert.AreEqual(3,session.occupants.Count(n=>n.role==OccupantRole.Teacher));Assert.AreEqual(3,session.occupants.Count(n=>n.role==OccupantRole.Staff));
            Assert.AreEqual(1,session.occupants.Count(n=>n.corridorDuty));
            Assert.IsTrue(session.occupants.Where(n=>!n.corridorDuty).All(n=>n.workplaceDoor!=null));
            Assert.IsTrue(session.occupants.Where(n=>n.role==OccupantRole.Student).All(n=>n.seated&&n.transform.position.x>2));
        }
        [Test] public void NearestExitChangesWithPositionAndBlockedDoors()
        {
            TrainingSceneBuilder.Build();var session=Object.FindObjectOfType<TrainingSession>();var graph=session.graph;
            int nearLift=graph.Nearest(new Vector3(0,0,.85f));int nearMain=graph.Nearest(new Vector3(0,0,13));
            var liftPath=graph.FindPath(nearLift,n=>n.kind==RouteKind.Outside,false,true);
            var mainPath=graph.FindPath(nearMain,n=>n.kind==RouteKind.Outside,false,true);
            Assert.IsTrue(graph.nodes[liftPath[liftPath.Count-1]].secondaryExit);Assert.IsFalse(graph.nodes[mainPath[mainPath.Count-1]].secondaryExit);
            var blockedMain=graph.FindPath(nearMain,n=>n.kind==RouteKind.Outside,false,true,true);
            Assert.IsTrue(graph.nodes[blockedMain[blockedMain.Count-1]].secondaryExit);
            var blockedSecond=graph.FindPath(nearLift,n=>n.kind==RouteKind.Outside,false,true,false,null,true);
            Assert.IsFalse(graph.nodes[blockedSecond[blockedSecond.Count-1]].secondaryExit);
            Assert.IsEmpty(graph.FindPath(nearLift,n=>n.kind==RouteKind.Outside,false,true,true,null,true));
            int upper=graph.Nearest(new Vector3(0,7,4));Assert.IsEmpty(graph.FindPath(upper,n=>n.kind==RouteKind.Outside,true,true));
            Physics.SyncTransforms();int architecture=~((1<<5)|(1<<8)|(1<<9));
            Assert.IsFalse(Physics.Raycast(new Vector3(0,1,.85f),Vector3.left,8,architecture),"Second exit passage must be physically clear");
        }
    }
}
