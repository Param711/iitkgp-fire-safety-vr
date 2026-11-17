using System.Text;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace FireSafety
{
    public class TrainingPresentation : MonoBehaviour
    {
        public TrainingSession session;
        public Transform panel;
        public TextMesh heading,body,footer;
        public TrainingInteractable[] buttons;
        public TextMesh[] buttonLabels;
        public bool Visible {get;private set;}
        public int page;
        const int TimelinePerPage=6;
        bool showingMenu;
        enum TheoryView { None, Lesson, Question, Feedback, Results }
        TheoryView theoryView;
        int theoryPage,quizIndex,quizCorrect;
        void LateUpdate()
        {
            if(showingMenu&&Visible)footer.text=session.player.ReadinessSummary;
        }
        void Place()
        {
            session.player.LevelDesktopView();
            var head=session.player.Head;
            Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<0.1f)forward=Vector3.forward;
            panel.position=head.position+forward*2.0f+Vector3.down*0.1f;
            panel.rotation=Quaternion.LookRotation(forward,Vector3.up);
            panel.gameObject.SetActive(true);Visible=true;
            if(!session.player.IsVR){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        public void Hide(){panel.gameObject.SetActive(false);Visible=false;showingMenu=false;}
        void ClearButtons(){foreach(var b in buttons)b.gameObject.SetActive(false);theoryView=TheoryView.None;}
        void Button(int slot,string text,InteractionAction action,int value=0)
        {var b=buttons[slot];b.gameObject.SetActive(true);b.action=action;b.value=value;buttonLabels[slot].text=text;}
        public void ShowMenu()
        {
            if(session.phase==SessionPhase.Assessing){TogglePausePanel();return;}
            Place();ClearButtons();heading.text="FIRE SAFETY  /  LEARN AND PLAY";
            showingMenu=true;if(session.audioDirector!=null)session.audioDirector.Stop();
            body.text="IIT KHARAGPUR / INDUSTRIAL & SYSTEMS ENGINEERING\n\n1  THEORY & QUIZ\nLearn PASS and fire responses, then answer the quiz.\n\n2  PLAY\nChoose difficulty, fire type, intensity, place and floor,\nor generate a fresh random combination.\nReview detailed analysis and recommendations afterwards.\n\n3  CONTROLS\nReview the controls for PC or VR and check the sound.\n\n"+(session.player.IsVR?"Point your right-controller ray and press its trigger.":"Click an option. Press Esc to release the mouse cursor.");
            footer.text=session.player.ReadinessSummary;FitBody();
            Button(0,"THEORY & QUIZ",InteractionAction.TheoryPage,0);Button(1,"PLAY",InteractionAction.AssessmentList);
            Button(2,"CONTROLS",InteractionAction.Controls,session.player.IsVR?1:0);
        }
        public void ShowAssessmentSelection()
        {
            if(session.phase==SessionPhase.Assessing)return;
            Place();ClearButtons();heading.text="PLAY  /  CHOOSE YOUR GAME";showingMenu=true;
            if(session.audioDirector!=null)session.audioDirector.Stop();
            body.text="CUSTOM GAME\nSet difficulty, fire type, intensity, location and floor.\n\nRANDOM GAME\nIndependently draws all five settings for a new incident.\nRandom draws can repeat; they do not cycle seven presets.\nRead the generated settings before beginning.\n\nDecisions are assessed without live correctness hints.\nDetailed analysis and recommendations follow each game.\n\nREPORT / BUILDING shows your latest saved result\nand where to find the washrooms and water.";
            footer.text=session.player.ReadinessSummary;FitBody();
            Button(0,"CUSTOM GAME",InteractionAction.GameSetup,0);
            Button(1,"RANDOM GAME",InteractionAction.GameRandom);
            Button(2,"REPORT / BUILDING",InteractionAction.GameMore);
        }
        public void ShowGameMore()
        {
            if(session.phase==SessionPhase.Assessing)return;
            Place();ClearButtons();showingMenu=false;heading.text="BUILDING  /  REPORT";
            body.text="ON EVERY FLOOR\n\nFrom the lift, walk along the main corridor to its end.\nWomen: continue STRAIGHT through the end door.\nMen: turn RIGHT at the washroom branch.\nSigns mark the branch and each washroom door.\nBuckets and working taps are inside both washrooms.\n\nThe water cooler is opposite the lift on every floor.\nThere is ONE staircase and TWO ground-floor exits.\nThe main entrance faces the stairs (four metres wide).\nThe second exit is right of the lift, facing its doors.\n\nLAST REPORT reopens the most recently saved analysis.";
            footer.text="Directions follow your confirmation / room distances remain provisional";FitBody();
            if(session.history.Count>0)Button(0,"LAST REPORT",InteractionAction.LastReport);
            Button(1,"BACK TO PLAY",InteractionAction.AssessmentList);Button(2,"HOME",InteractionAction.Menu);
        }
        public void ShowGameSetup(int field)
        {
            if(session.phase==SessionPhase.Assessing)return;
            field=Mathf.Clamp(field,0,5);Place();ClearButtons();showingMenu=false;
            var s=session.gameSettings;
            string[] names={"DIFFICULTY","FIRE TYPE","INTENSITY","LOCATION","FLOOR"};
            string[] values={TrainingGameSettings.Difficulties[s.difficulty],TrainingInstructions.FireName(s.fireKind),TrainingGameSettings.Intensities[s.intensity],IncidentSites.Names[s.location],IncidentSites.FloorName(IncidentSites.GroundOnly(s.location)?0:s.floor)};
            heading.text=field==5?"CUSTOM GAME  /  READY":"CUSTOM GAME  /  "+(field+1)+" OF 5";body.text="";
            for(int i=0;i<5;i++)body.text+=(field==i?"> ":"  ")+names[i]+": "+values[i]+"\n\n";
            body.text+="Easy / Medium / Hard changes development speed\nand the trial time limit, without adding live hints.\nSmall: incipient. Growing: develops if uncontrolled.\nLarge: already beyond portable-equipment limits.\nBoth exit locations always use the ground floor.";
            footer.text=field==5?"Review settings, then select PLAY / the clock has not started":"CHANGE cycles this setting / NEXT confirms and moves on";FitBody();
            if(field<5){Button(0,"CHANGE "+names[field],InteractionAction.GameCycle,field);Button(1,"NEXT",InteractionAction.GameSetup,field+1);}
            else{Button(0,"PLAY",InteractionAction.GamePrepare);Button(1,"EDIT SETTINGS",InteractionAction.GameSetup,0);}
            Button(2,field==0?"BACK TO PLAY":"PREVIOUS",field==0?InteractionAction.AssessmentList:InteractionAction.GameSetup,field-1);
        }
        public void CycleGameSetting(int field)
        {
            if(session.phase==SessionPhase.Assessing)return;
            var s=session.gameSettings;
            switch(field){case 0:s.difficulty=(s.difficulty+1)%3;break;case 1:s.fireKind=(FireKind)(((int)s.fireKind+1)%3);break;
                case 2:s.intensity=(s.intensity+1)%3;break;case 3:s.location=(s.location+1)%IncidentSites.Names.Length;break;case 4:s.floor=(s.floor+1)%3;break;}
            ShowGameSetup(field);
        }
        public void ShowBriefing()
        {
            Place();ClearButtons();showingMenu=false;heading.text="INCIDENT BRIEFING";
            var scenario=session.scenarios[session.PendingScenarioIndex];
            body.text=scenario.id+"\n\nFIRE TYPE\n"+TrainingInstructions.FireName(scenario.fireKind)+"\n\n"+
                Wrap(scenario.title,73)+"\n\nYou will respond to this incident in the department.\nUse CONTROLS if you need to review the inputs.\n\n"+
                "The assessment clock and fire have not started.\nSelect BEGIN when ready.\n\n"+
                "During the trial there are no live correctness hints.\nYour result and decision timeline appear afterwards.\n\n"+
                "Audio includes spoken briefings, nearby occupants,\nfire sounds and the alarm after activation.";
            footer.text=session.player.ReadinessSummary;FitBody();
            Button(0,"BEGIN",InteractionAction.BeginBriefing);Button(1,"CONTROLS",InteractionAction.Controls,session.player.IsVR?1:0);
            Button(2,"BACK",InteractionAction.AssessmentList);
            if(Application.isPlaying&&session.audioDirector!=null)session.audioDirector.Brief(scenario.fireKind);
        }
        public void ShowControls(bool vr)
        {
            if(session.phase==SessionPhase.Assessing)return;
            Place();ClearButtons();showingMenu=false;heading.text=vr?"VR CONTROLS":"DESKTOP CONTROLS";
            body.text=TrainingInstructions.Controls(vr);footer.text="Controls orientation / assessment has not started";FitBody();
            Button(0,vr?"SHOW PC CONTROLS":"SHOW VR CONTROLS",InteractionAction.Controls,vr?0:1);
            Button(1,"SOUND CHECK",InteractionAction.SoundCheck);Button(2,"BACK",InteractionAction.BackToBriefing);
            if(Application.isPlaying&&session.audioDirector!=null)session.audioDirector.Controls(vr);
        }
        public void ShowEquipmentGuide()
        {
            if(session.phase==SessionPhase.Assessing)return;
            Place();ClearButtons();showingMenu=false;heading.text="EQUIPMENT ORIENTATION";body.text=TrainingInstructions.Equipment;
            footer.text="Before assessment / local equipment rating still needs verification";FitBody();
            Button(0,"DESKTOP CONTROLS",InteractionAction.Controls,0);Button(1,"VR CONTROLS",InteractionAction.Controls,1);
            Button(2,"BACK",InteractionAction.BackToBriefing);
            if(Application.isPlaying&&session.audioDirector!=null)session.audioDirector.Play(session.audioDirector.equipment);
        }
        public void BackToOrientation()
        {if(session.phase==SessionPhase.Assessing)return;if(session.phase==SessionPhase.Briefing)ShowBriefing();else ShowMenu();}
        public void ShowTheory(int requested)
        {
            if(session.phase==SessionPhase.Assessing)return;
            var lessons=TrainingTheory.Course.pages;theoryPage=Mathf.Clamp(requested,0,lessons.Length-1);
            Place();ClearButtons();showingMenu=false;theoryView=TheoryView.Lesson;
            heading.text="THEORY  /  "+(theoryPage+1)+" OF "+lessons.Length;
            body.text=lessons[theoryPage].title+"\n\n"+lessons[theoryPage].body;footer.text="Theory first / the quiz follows the last lesson";FitBody();
            if(theoryPage>0)Button(0,"PREVIOUS LESSON",InteractionAction.TheoryPage,theoryPage-1);
            else Button(0,"BACK",InteractionAction.Menu);
            if(theoryPage<lessons.Length-1)Button(1,"NEXT LESSON",InteractionAction.TheoryPage,theoryPage+1);
            else Button(1,"TAKE QUIZ",InteractionAction.TheoryQuiz);
            Button(2,"REPLAY VOICE",InteractionAction.TheoryReplay);ReplayTheory();
        }
        public void ReplayTheory()
        {
            if(session.phase==SessionPhase.Assessing||!Application.isPlaying||session.audioDirector==null)return;
            var audio=session.audioDirector;
            if(theoryView==TheoryView.Lesson)audio.Play(audio.theoryLessons[theoryPage]);
            else if(theoryView==TheoryView.Question)audio.Play(audio.theoryQuestions[quizIndex]);
            else if(theoryView==TheoryView.Feedback)audio.Play(audio.theoryAnswers[quizIndex]);
        }
        public void BeginTheoryQuiz()
        {
            if(session.phase==SessionPhase.Assessing)return;
            quizIndex=quizCorrect=0;ShowTheoryQuestion();
        }
        void ShowTheoryQuestion()
        {
            var questions=TrainingTheory.Course.questions;var question=questions[quizIndex];
            Place();ClearButtons();showingMenu=false;theoryView=TheoryView.Question;
            heading.text="PRACTICE CHECK  /  "+(quizIndex+1)+" OF "+questions.Length;
            body.text=Wrap(question.prompt,73)+"\n\nChoose one answer below.\nYou will see an explanation after answering.";
            footer.text="Learning practice / separate from assessment scoring";FitBody();
            for(int i=0;i<question.answers.Length;i++)Button(i,question.answers[i],InteractionAction.TheoryAnswer,i);
            ReplayTheory();
        }
        public void AnswerTheory(int answer)
        {
            if(session.phase==SessionPhase.Assessing||theoryView!=TheoryView.Question)return;
            var question=TrainingTheory.Course.questions[quizIndex];if(answer<0||answer>=question.answers.Length)return;
            bool correct=answer==question.correctAnswer;if(correct)quizCorrect++;
            Place();ClearButtons();theoryView=TheoryView.Feedback;
            heading.text=correct?"PRACTICE  /  CORRECT":"PRACTICE  /  REVIEW THIS RESPONSE";
            body.text=Wrap(question.prompt,73)+"\n\nYou chose: "+question.answers[answer]+"\nCorrect response: "+question.answers[question.correctAnswer]+"\n\n"+question.explanation;
            footer.text="Read the explanation, then continue / assessment score unchanged";FitBody();
            Button(0,"REPLAY VOICE",InteractionAction.TheoryReplay);Button(1,"CONTINUE",InteractionAction.TheoryNext);
            Button(2,"MENU",InteractionAction.Menu);ReplayTheory();
        }
        public void NextTheoryQuestion()
        {
            if(session.phase==SessionPhase.Assessing||theoryView!=TheoryView.Feedback)return;
            if(++quizIndex<TrainingTheory.Course.questions.Length){ShowTheoryQuestion();return;}
            Place();ClearButtons();theoryView=TheoryView.Results;
            heading.text="PRACTICE CHECK  /  COMPLETED";
            body.text="Practice result: "+quizCorrect+" / "+TrainingTheory.Course.questions.Length+"\n\nReview any missed topics and repeat the theory if useful.\n\nThis is a learning check, separate from assessment scoring.\nA practice result does not certify firefighting competence.\n\nChoose PLAY when you are ready.";
            footer.text="Theory complete / no assessment clock or incident was started";FitBody();
            Button(0,"PLAY",InteractionAction.AssessmentList);Button(1,"REPEAT THEORY",InteractionAction.TheoryPage,0);Button(2,"MENU",InteractionAction.Menu);
            if(session.audioDirector!=null)session.audioDirector.Stop();
        }
        public void TogglePausePanel()
        {
            if(Visible){Hide();return;}
            Place();ClearButtons();heading.text="SESSION CONTROLS";
            showingMenu=false;
            body.text=TrainingInstructions.Controls(session.player.IsVR);
            footer.text="Assessment clock continues / Tab or left menu closes controls";
            FitBody();
            Button(0,"RESUME",InteractionAction.ClosePanel);Button(1,"END ASSESSMENT",InteractionAction.EndAssessment);
        }
        public void ShowReport(int requested)
        {
            var r=session.report;if(r==null)return;
            showingMenu=false;
            var reviewPages=BuildReviewPages(r);
            var coursePages=TrainingCourseAnalysis.Pages(r);
            int courseStart=3+reviewPages.Count;
            int timelineStart=courseStart+coursePages.Count;
            int totalPages=timelineStart+Mathf.CeilToInt(r.events.Count/(float)TimelinePerPage);
            page=Mathf.Clamp(requested,0,totalPages-1);Place();ClearButtons();
            heading.text="SESSION ANALYSIS  /  "+(page+1)+" OF "+totalPages;
            var b=new StringBuilder();
            if(page==0)
            {
                b.AppendLine(r.score+" / 100    "+r.classification+"\n");
                b.AppendLine(r.scenarioId+"  "+r.scenario+"\n");
                b.AppendLine(r.outcome+"\n");b.AppendLine("Elapsed: "+AssessmentReport.F(r.durationSeconds)+" s    Distance: "+AssessmentReport.F(r.distanceMeters)+" m");
                b.AppendLine("First response: "+(r.firstResponseSeconds<0?"Not recorded":AssessmentReport.F(r.firstResponseSeconds)+" s"));
                b.AppendLine("Alarm: "+(r.alarmSeconds<0?"Not activated":AssessmentReport.F(r.alarmSeconds)+" s"));
                b.AppendLine("Assistance: "+(r.assistanceSeconds<0?"Not requested":AssessmentReport.F(r.assistanceSeconds)+" s"));
                b.AppendLine("Route changes: "+r.routeChanges+"    Unsafe decisions: "+r.unsafeDecisions);
                b.AppendLine("Heat: "+AssessmentReport.F(r.heatExposureSeconds)+" s    Smoke: "+AssessmentReport.F(r.smokeExposureSeconds)+" s");
                b.AppendLine("Source suppressed: "+(r.extinguishedSeconds<0?"Not recorded":AssessmentReport.F(r.extinguishedSeconds)+" s"));
                b.AppendLine("Spread zones reached: "+r.spreadZonesActivated);
                b.AppendLine("NPCs at assembly: "+r.npcsEvacuated+" / "+r.npcCount+"    Awaiting help: "+r.npcsAtRefuge);
                b.AppendLine("NPC exits: main "+r.npcsMainExit+" / beside lift "+r.npcsSecondExit);
                if(r.waterCollectedLiters>0||r.bucketUsed)b.AppendLine("Bucket: "+AssessmentReport.F(r.waterCollectedLiters)+" L collected; "+AssessmentReport.F(r.waterEffectiveLiters)+" L effective");
            }
            else if(page==1)
            {
                foreach(var c in r.categories){b.AppendLine(c.name+"    "+c.earned+" / "+c.maximum);b.AppendLine(Wrap(c.evidence,73)+"\n");}
            }
            else if(page==2)
            {
                b.AppendLine("EQUIPMENT AND FIRE DEVELOPMENT\n");
                b.AppendLine("Extinguisher used: "+(r.extinguisherAttempted?"Yes":"No / PASS not assessed"));
                b.AppendLine("Pin: "+(r.pinPulled?"Yes":"No")+"    Base aim: "+(r.aimedAtBase?"Yes":"No"));
                b.AppendLine("Squeeze: "+(r.squeezed?"Yes":"No")+"    Sweep: "+(r.swept?"Yes":"No"));
                b.AppendLine("PASS order: "+(!r.extinguisherAttempted?"Not assessed":!r.passInOrder?"Sequence error":r.pinPulled&&r.aimedAtBase&&r.squeezed&&r.swept?"Complete in order":"Incomplete"));
                b.AppendLine("Spray: "+AssessmentReport.F(r.spraySeconds)+" s    Effective: "+AssessmentReport.F(r.effectiveSpraySeconds)+" s");
                b.AppendLine("Cylinder profile: "+(r.extinguisherProfileVerified?"Verified":"Unverified"));
                b.AppendLine("Source suppressed: "+(r.extinguishedSeconds<0?"Not recorded":AssessmentReport.F(r.extinguishedSeconds)+" s"));
                b.AppendLine("Spread zones: "+r.spreadZonesActivated+"    Recorded door delay: "+AssessmentReport.F(r.externalSpreadDelaySeconds)+" s");
                b.AppendLine("Initial severity: "+(!r.fireDevelopmentRecorded?"Not recorded":r.fireWasInitiallyDangerous?"Already dangerous":"Initially small"));
                b.AppendLine("Bucket collected: "+AssessmentReport.F(r.waterCollectedLiters)+" L    Poured: "+AssessmentReport.F(r.waterPouredLiters)+" L");
                b.AppendLine("Effective water: "+AssessmentReport.F(r.waterEffectiveLiters)+" L    Unsafe water uses: "+r.unsafeWaterUses);
            }
            else if(page<courseStart)
            {
                b.Append(reviewPages[page-3]);
            }
            else if(page<timelineStart)
            {
                b.Append(coursePages[page-courseStart]);
            }
            else
            {
                b.AppendLine("OBSERVED DECISIONS\n");int start=(page-timelineStart)*TimelinePerPage;
                for(int i=start;i<Mathf.Min(start+TimelinePerPage,r.events.Count);i++){var e=r.events[i];b.AppendLine(AssessmentReport.F(e.seconds)+"s  "+e.action+" ["+e.severity+"]");b.AppendLine(Wrap(e.detail,73));}
            }
            body.text=b.ToString();
            FitBody();
            footer.text=string.IsNullOrEmpty(session.LatestReportSaveError)?"Review your result here / use NEXT and PREVIOUS to read all pages":"Latest result could not be saved / review it before closing";
            if(page>0)Button(0,"PREVIOUS PAGE",InteractionAction.ReportPage,page-1);
            if(page<totalPages-1)Button(1,"NEXT PAGE",InteractionAction.ReportPage,page+1);
            Button(2,"DONE",InteractionAction.Next);
        }
        public void OpenReport()
        {
            if(session.report==null || string.IsNullOrEmpty(session.report.exportDirectory))return;
            string path=Path.Combine(session.report.exportDirectory,"analysis.html");
            if(File.Exists(path))Application.OpenURL(new System.Uri(path).AbsoluteUri);
        }
        void FitBody()
        {int lines=body.text.Split('\n').Length;body.characterSize=.014f*Mathf.Min(1,20f/Mathf.Max(1,lines));}
        static List<string> BuildReviewPages(AssessmentReport report)
        {
            var pages=new List<string>();var current=new StringBuilder("RECOMMENDATIONS\n\n");int lines=2;
            foreach(var point in report.recommendations)
            {
                string text=Wrap("- "+point,73);int count=text.Split('\n').Length+1;
                if(lines+count>18&&lines>2){pages.Add(current.ToString());current.Clear();current.Append("RECOMMENDATIONS / CONTINUED\n\n");lines=2;}
                current.AppendLine(text).AppendLine();lines+=count;
            }
            pages.Add(current.ToString());return pages;
        }
        static string Wrap(string text,int limit)
        {
            var b=new StringBuilder();int line=0;
            foreach(var word in text.Split(' ')){if(line+word.Length>limit){b.Append('\n');line=0;}b.Append(word).Append(' ');line+=word.Length+1;}
            return b.ToString();
        }
    }
}
