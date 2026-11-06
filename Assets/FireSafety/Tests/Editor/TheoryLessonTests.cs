using System.Linq;
using FireSafety.Editor;
using NUnit.Framework;
using UnityEngine;

namespace FireSafety.Tests
{
    public class TheoryLessonTests
    {
        TrainingSession session;
        [OneTimeSetUp] public void BuildScene(){TrainingSceneBuilder.Build();session=Object.FindObjectOfType<TrainingSession>();}
        [TearDown] public void Reset(){session.ReturnToMenu();}
        [Test] public void EveryLessonAndQuestionHasReadableContentAndMatchingVoice()
        {
            var course=TrainingTheory.Course;var audio=session.audioDirector;
            Assert.AreEqual(7,course.pages.Length);Assert.AreEqual(4,course.questions.Length);
            Assert.AreEqual(course.pages.Length,audio.theoryLessons.Length);Assert.AreEqual(course.questions.Length,audio.theoryQuestions.Length);
            Assert.AreEqual(course.questions.Length,audio.theoryAnswers.Length);
            for(int i=0;i<course.pages.Length;i++)
            {
                var lesson=course.pages[i];Assert.IsNotEmpty(lesson.title);Assert.IsNotEmpty(lesson.narration);
                Assert.LessOrEqual(lesson.body.Split('\n').Length,20);Assert.IsTrue(lesson.body.Split('\n').All(line=>line.Length<=73));
                Assert.Greater(audio.theoryLessons[i].length,1);session.presentation.ShowTheory(i);Assert.GreaterOrEqual(session.presentation.body.characterSize,.013f);
            }
            for(int i=0;i<course.questions.Length;i++)
            {
                var question=course.questions[i];Assert.AreEqual(3,question.answers.Length);
                Assert.That(question.correctAnswer,Is.InRange(0,2));Assert.IsNotEmpty(question.explanation);Assert.IsNotEmpty(question.narration);
                Assert.Greater(audio.theoryQuestions[i].length,1);Assert.Greater(audio.theoryAnswers[i].length,1);
            }
        }
        [Test] public void HomeOffersLearningAndAssessmentsAndControlsReturnHome()
        {
            session.phase=SessionPhase.Ready;session.presentation.ShowMenu();
            Assert.AreEqual(InteractionAction.TheoryPage,session.presentation.buttons[0].action);
            Assert.AreEqual(InteractionAction.AssessmentList,session.presentation.buttons[1].action);
            Assert.AreEqual(InteractionAction.Controls,session.presentation.buttons[2].action);
            Assert.AreEqual(3,session.presentation.buttons.Length);
            session.presentation.ShowControls(false);Assert.AreEqual(InteractionAction.BackToBriefing,session.presentation.buttons[2].action);
            session.presentation.BackToOrientation();StringAssert.Contains("LEARN AND PLAY",session.presentation.heading.text);
            session.presentation.ShowAssessmentSelection();Assert.AreEqual(InteractionAction.GameSetup,session.presentation.buttons[0].action);
        }
        [Test] public void EveryScreenHasAtMostThreeVisibleOptionsAndQuizFollowsTheory()
        {
            Assert.AreEqual(3,session.presentation.buttons.Length);
            session.presentation.ShowMenu();Assert.AreEqual(3,session.presentation.buttons.Count(b=>b.gameObject.activeSelf));
            for(int i=0;i<TrainingTheory.Course.pages.Length;i++)
            {session.presentation.ShowTheory(i);Assert.LessOrEqual(session.presentation.buttons.Count(b=>b.gameObject.activeSelf),3);}
            Assert.AreEqual(InteractionAction.TheoryQuiz,session.presentation.buttons[1].action);
            session.presentation.buttons[1].Activate(session.player);
            Assert.AreEqual(3,session.presentation.buttons.Count(b=>b.gameObject.activeSelf));
            Assert.IsTrue(session.presentation.buttons.All(b=>b.action==InteractionAction.TheoryAnswer));
            session.presentation.AnswerTheory(0);Assert.LessOrEqual(session.presentation.buttons.Count(b=>b.gameObject.activeSelf),3);
            session.PrepareAssessment(0);Assert.LessOrEqual(session.presentation.buttons.Count(b=>b.gameObject.activeSelf),3);
            session.presentation.ShowControls(true);Assert.LessOrEqual(session.presentation.buttons.Count(b=>b.gameObject.activeSelf),3);
        }
        [Test] public void VariedAssessmentDoesNotRepeatUntilAllScenariosAreOffered()
        {
            var offered=new System.Collections.Generic.HashSet<int>();
            for(int i=0;i<session.scenarios.Length;i++){session.PrepareVariedAssessment();Assert.IsTrue(offered.Add(session.PendingScenarioIndex));}
            Assert.AreEqual(session.scenarios.Length,offered.Count);
            Assert.IsTrue(session.scenarios.Any(s=>s.initialFootprint>1.45f&&s.primaryBlocked));
            Assert.AreEqual(3,session.scenarios.Select(s=>s.incidentRoom).Distinct().Count());
        }
        [Test] public void TheoryReturnsToSamePendingScenarioWithoutStartingIt()
        {
            session.PrepareAssessment(3);session.presentation.ShowTheory(0);
            Assert.AreEqual(SessionPhase.Briefing,session.phase);Assert.IsFalse(session.incident.Active);Assert.AreEqual(0,session.Elapsed);
            session.presentation.ShowTheory(5);StringAssert.Contains("only one staircase",session.presentation.body.text);
            session.presentation.BackToOrientation();
            Assert.AreEqual(3,session.PendingScenarioIndex);StringAssert.Contains("ENERGIZED ELECTRICAL",session.presentation.body.text);
            Assert.AreEqual(InteractionAction.BeginBriefing,session.presentation.buttons[0].action);
        }
        [Test] public void QuizExplainsWrongAnswersAndIgnoresRepeatedAnswerClicks()
        {
            session.PrepareAssessment(0);
            var report=new AssessmentReport{score=87,outcome="Existing assessment result"};session.report=report;
            string before=JsonUtility.ToJson(report);int historyCount=session.history.Count;
            session.presentation.BeginTheoryQuiz();session.presentation.AnswerTheory(0);
            StringAssert.Contains("Correct response: FIRE BASE",session.presentation.body.text);
            session.presentation.AnswerTheory(1);session.presentation.NextTheoryQuestion();
            for(int i=1;i<TrainingTheory.Course.questions.Length;i++)
            {session.presentation.AnswerTheory(TrainingTheory.Course.questions[i].correctAnswer);session.presentation.NextTheoryQuestion();}
            StringAssert.Contains("Practice result: 3 / 4",session.presentation.body.text);
            Assert.AreEqual(before,JsonUtility.ToJson(report));Assert.AreEqual(historyCount,session.history.Count);
            Assert.AreEqual(SessionPhase.Briefing,session.phase);Assert.IsFalse(session.incident.Active);
            session.presentation.BeginTheoryQuiz();StringAssert.Contains("1 OF 4",session.presentation.heading.text);
        }
    }
}
