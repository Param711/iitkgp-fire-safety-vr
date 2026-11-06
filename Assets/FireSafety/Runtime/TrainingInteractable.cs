using UnityEngine;

namespace FireSafety
{
    public enum InteractionAction { Alarm, Assistance, Door, GrabExtinguisher, PullPin, LiftFloor, LiftDoor,
        Start, Battery, ReportPage, Next, Menu, EndAssessment, OpenReport, GrabBucket, ToggleWater,
        Briefing, BriefingBattery, BeginBriefing, ReplayBriefing, Controls, EquipmentGuide, BackToBriefing, SoundCheck,
        AssessmentList, TheoryPage, TheoryReplay, TheoryQuiz, TheoryAnswer, TheoryNext, LastReport, VariedAssessment, ClosePanel,
        GameSetup, GameCycle, GameRandom, GamePrepare, GameMore }
    public class TrainingInteractable : MonoBehaviour
    {
        public TrainingSession session;
        public InteractionAction action;
        public int value;
        public TrainingDoor door;
        public TrainingExtinguisher tool;
        public TrainingBucket bucket;
        public TrainingWaterSource waterSource;
        public string objectLabel;
        public void Activate(TrainingXRPlayer player)
        {
            switch(action)
            {
                case InteractionAction.Alarm:session.RaiseAlarm();break;
                case InteractionAction.Assistance:session.CallAssistance();break;
                case InteractionAction.Door:if(door!=null)door.Toggle();break;
                case InteractionAction.GrabExtinguisher:if(tool!=null)player.Grab(tool);break;
                case InteractionAction.PullPin:if(tool!=null && player.HeldTool==tool)tool.PullPin();break;
                case InteractionAction.LiftFloor:session.lift.RequestFloor(value,true);break;
                case InteractionAction.LiftDoor:session.lift.ToggleDoors();break;
                case InteractionAction.Start:session.StartAssessment(value);break;
                case InteractionAction.Battery:session.StartAssessment(0,true);break;
                case InteractionAction.ReportPage:session.presentation.ShowReport(value);break;
                case InteractionAction.Next:session.Next();break;
                case InteractionAction.Menu:session.ReturnToMenu();break;
                case InteractionAction.EndAssessment:session.Finish("Assessment ended by participant");break;
                case InteractionAction.OpenReport:session.presentation.OpenReport();break;
                case InteractionAction.GrabBucket:if(bucket!=null)player.GrabBucket(bucket);break;
                case InteractionAction.ToggleWater:if(waterSource!=null)waterSource.Toggle();break;
                case InteractionAction.Briefing:session.PrepareAssessment(value);break;
                case InteractionAction.BriefingBattery:session.PrepareAssessment(0,true);break;
                case InteractionAction.BeginBriefing:session.BeginPreparedAssessment();break;
                case InteractionAction.ReplayBriefing:session.audioDirector.Brief(session.scenarios[session.PendingScenarioIndex].fireKind);break;
                case InteractionAction.Controls:session.presentation.ShowControls(value==1);break;
                case InteractionAction.EquipmentGuide:session.presentation.ShowEquipmentGuide();break;
                case InteractionAction.BackToBriefing:session.presentation.BackToOrientation();break;
                case InteractionAction.SoundCheck:session.audioDirector.Play(session.audioDirector.soundCheck);break;
                case InteractionAction.AssessmentList:session.presentation.ShowAssessmentSelection();break;
                case InteractionAction.TheoryPage:session.presentation.ShowTheory(value);break;
                case InteractionAction.TheoryReplay:session.presentation.ReplayTheory();break;
                case InteractionAction.TheoryQuiz:session.presentation.BeginTheoryQuiz();break;
                case InteractionAction.TheoryAnswer:session.presentation.AnswerTheory(value);break;
                case InteractionAction.TheoryNext:session.presentation.NextTheoryQuestion();break;
                case InteractionAction.LastReport:session.ShowLatestReport();break;
                case InteractionAction.VariedAssessment:session.PrepareVariedAssessment();break;
                case InteractionAction.ClosePanel:session.presentation.Hide();break;
                case InteractionAction.GameSetup:session.presentation.ShowGameSetup(value);break;
                case InteractionAction.GameCycle:session.presentation.CycleGameSetting(value);break;
                case InteractionAction.GameRandom:session.PrepareGame(true);break;
                case InteractionAction.GamePrepare:session.PrepareGame(false);break;
                case InteractionAction.GameMore:session.presentation.ShowGameMore();break;
            }
            if(action==InteractionAction.Alarm || action==InteractionAction.Assistance)player.Haptic(0.25f,0.08f);
        }
    }
}
