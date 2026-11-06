using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FireSafety.Tests
{
    public class AssessmentRulesTests
    {
        [Test] public void WaterCannotTreatElectricalOrLiquidFire()
        {
            Assert.IsTrue(AssessmentRules.Compatible(AgentKind.Water,FireKind.OrdinaryCombustibles));
            Assert.IsFalse(AssessmentRules.Compatible(AgentKind.Water,FireKind.EnergizedElectrical));
            Assert.IsFalse(AssessmentRules.Compatible(AgentKind.Water,FireKind.FlammableLiquid));
        }
        [Test] public void CarbonDioxideIsNotCreditedForOrdinaryCombustibles()
        {
            Assert.IsFalse(AssessmentRules.Compatible(AgentKind.CarbonDioxide,FireKind.OrdinaryCombustibles));
            Assert.IsTrue(AssessmentRules.Compatible(AgentKind.CarbonDioxide,FireKind.EnergizedElectrical));
            Assert.IsTrue(AssessmentRules.Compatible(AgentKind.CarbonDioxide,FireKind.FlammableLiquid));
        }
        [Test] public void DryChemicalHandlesEveryConfiguredIncident()
        {foreach(FireKind kind in Enum.GetValues(typeof(FireKind)))Assert.IsTrue(AssessmentRules.Compatible(AgentKind.DryChemicalABC,kind));}
        [Test] public void RefugeRequiresEverySafeguard()
        {
            Assert.IsTrue(AssessmentRules.RefugeSuccess(true,true,true,true,true));
            for(int missing=0;missing<5;missing++)
            {bool[] checks={true,true,true,true,true};checks[missing]=false;Assert.IsFalse(AssessmentRules.RefugeSuccess(checks[0],checks[1],checks[2],checks[3],checks[4]));}
        }
        static List<RouteNode> Network()
        {
            return new List<RouteNode>{
                new RouteNode{id="start",position=new Vector3(0,7,0),neighbors=new List<int>{1,2,4}},
                new RouteNode{id="A",position=new Vector3(1,3.5f,0),kind=RouteKind.PrimaryStairs,neighbors=new List<int>{0,3}},
                new RouteNode{id="B",position=new Vector3(10,3.5f,0),kind=RouteKind.SecondaryStairs,neighbors=new List<int>{0,3}},
                new RouteNode{id="assembly",position=new Vector3(0,0,0),kind=RouteKind.Assembly,neighbors=new List<int>{1,2}},
                new RouteNode{id="refuge",position=new Vector3(-3,7,0),kind=RouteKind.Refuge,neighbors=new List<int>{0}}
            };
        }
        [Test] public void PlannerChoosesShortestAvailableStair()
        {CollectionAssert.AreEqual(new[]{0,1,3},RoutePlanner.FindPath(Network(),0,n=>n.kind==RouteKind.Assembly,false,false));}
        [Test] public void PlannerUsesAlternativeWhenPrimaryBlocked()
        {CollectionAssert.AreEqual(new[]{0,2,3},RoutePlanner.FindPath(Network(),0,n=>n.kind==RouteKind.Assembly,true,false));}
        [Test] public void PlannerDoesNotInventEscapeWhenBothStairsBlocked()
        {Assert.IsEmpty(RoutePlanner.FindPath(Network(),0,n=>n.kind==RouteKind.Assembly,true,true));}
        [Test] public void PlannerCanReachRefugeWhenBothStairsBlocked()
        {CollectionAssert.AreEqual(new[]{0,4},RoutePlanner.FindPath(Network(),0,n=>n.kind==RouteKind.Refuge,true,true));}
        [Test] public void PlannerReturnsEmptyForInvalidStart()
        {Assert.IsEmpty(RoutePlanner.FindPath(Network(),-1,n=>true,false,false));}
        [Test] public void FastUnsafeEvacuationDoesNotGetSafeClassification()
        {
            var r=new AssessmentReport{outcome="Assembly",durationSeconds=5,unsafeDecisions=1};
            r.Evaluate(true,false,true,true);
            Assert.AreEqual("Completed with safety concerns",r.classification);Assert.Less(r.score,100);
        }
        [Test] public void SafeEvacuationDoesNotRequireFirefighting()
        {
            var r=new AssessmentReport{outcome="Assembly",extinguisherAttempted=false};r.Evaluate(true,false,true,true);
            Assert.AreEqual(100,r.score);Assert.AreEqual("Safe completion",r.classification);
            StringAssert.Contains("not assessed",r.categories[4].evidence);
        }
        [Test] public void IncompleteSessionCannotBeClassifiedAsSafe()
        {
            var r=new AssessmentReport{outcome="Timed out"};r.Evaluate(false,false,true,true);
            Assert.AreEqual("Incomplete / unsuccessful",r.classification);Assert.LessOrEqual(r.score,65);
        }
        [Test] public void RepeatedEvaluationDoesNotDuplicateCategories()
        {
            var r=new AssessmentReport();r.Evaluate(true,false,true,true);r.Evaluate(true,false,true,true);
            Assert.AreEqual(5,r.categories.Count);Assert.AreEqual(100,r.score);
        }
        [Test] public void ReportEscapesParticipantAndEventMarkup()
        {
            var r=new AssessmentReport{participantCode="<script>alert(1)</script>",outcome="<img src=x>"};
            r.events.Add(new AssessmentEvent{action="<unsafe>",detail="<script>",severity="unsafe"});r.Evaluate(false,false,false,false);
            var html=AssessmentExport.Html(r);StringAssert.DoesNotContain("<script>",html);StringAssert.Contains("&lt;script&gt;",html);StringAssert.DoesNotContain("<unsafe>",html);
        }
        [Test] public void DefaultScenariosHaveUniqueIdsAndCoverBlockedRoutes()
        {
            var ids=new HashSet<string>();var types=new HashSet<FireKind>();bool primary=false,exit=false,dynamic=false;
            foreach(var s in ScenarioDefinition.Defaults()){Assert.IsTrue(ids.Add(s.id));types.Add(s.fireKind);primary|=s.primaryBlocked;exit|=s.exitBlocked;dynamic|=s.developingFire;}
            Assert.AreEqual(3,types.Count);Assert.IsTrue(primary&&exit&&dynamic);
        }
        [Test] public void LocationsSeparateAssemblyRefugeAndBothStairs()
        {
            Assert.AreEqual(RouteKind.Assembly,TrainingSession.Locate(new Vector3(-16,0,13)));
            Assert.AreEqual(RouteKind.Refuge,TrainingSession.Locate(new Vector3(-4,7,22.5f)));
            Assert.AreEqual(RouteKind.PrimaryStairs,TrainingSession.Locate(new Vector3(3,5,13)));
            Assert.AreEqual(RouteKind.Washroom,TrainingSession.Locate(new Vector3(3,7,27)));
            Assert.AreEqual(RouteKind.Washroom,TrainingSession.Locate(new Vector3(0,7,32)));
            Assert.AreEqual(RouteKind.Corridor,TrainingSession.Locate(new Vector3(0,7,13)));
        }
        [Test] public void OutOfOrderPassCannotReceiveFullTechniqueCredit()
        {
            var r=new AssessmentReport{extinguisherAttempted=true,pinPulled=true,aimedAtBase=true,squeezed=true,swept=true,correctAgent=true,passInOrder=false};
            r.Evaluate(true,false,true,true);Assert.AreEqual(6,r.categories[4].earned);
        }
        [Test] public void NoHeadsetInitiallyPermitsDesktopRehearsal()
        {
            var state=VRTrackingPolicy.Evaluate(false,false,false,false,false,false);
            Assert.AreEqual(HeadsetState.Desktop,state);Assert.IsFalse(VRTrackingPolicy.Paused(state));
        }
        [Test] public void DisconnectedHeadsetCannotSilentlySwitchAnAssessmentToDesktop()
        {
            var state=VRTrackingPolicy.Evaluate(false,true,false,false,false,false);
            Assert.AreEqual(HeadsetState.Disconnected,state);Assert.IsTrue(VRTrackingPolicy.Paused(state));
        }
        [Test] public void RemovingTrackedHeadsetSuspendsAssessmentWhenPresenceIsAvailable()
        {
            var state=VRTrackingPolicy.Evaluate(true,true,true,true,true,false);
            Assert.AreEqual(HeadsetState.NotWorn,state);Assert.IsTrue(VRTrackingPolicy.Paused(state));
        }
        [Test] public void InvalidOrUntrackedHeadsetSuspendsAssessment()
        {
            Assert.AreEqual(HeadsetState.TrackingLost,VRTrackingPolicy.Evaluate(true,true,false,true,false,false));
            Assert.AreEqual(HeadsetState.TrackingLost,VRTrackingPolicy.Evaluate(true,true,true,false,true,true));
        }
        [Test] public void PresenceFeatureIsOptionalAndTrackedHeadsetCanResume()
        {
            var state=VRTrackingPolicy.Evaluate(true,true,true,true,false,false);
            Assert.AreEqual(HeadsetState.Ready,state);Assert.IsFalse(VRTrackingPolicy.Paused(state));
            Assert.AreEqual(HeadsetState.Ready,VRTrackingPolicy.Evaluate(true,true,true,true,true,true));
        }
        [Test] public void ShelterCompletionDoesNotClaimThatRescueOrEvacuationFinished()
        {
            var r=new AssessmentReport();r.Evaluate(false,true,true,true);
            Assert.AreEqual("Assistance requested; evacuation pending",r.classification);
            Assert.IsTrue(r.recommendations.Exists(s=>s.Contains("did not simulate a completed rescue")));
        }
    }
}
