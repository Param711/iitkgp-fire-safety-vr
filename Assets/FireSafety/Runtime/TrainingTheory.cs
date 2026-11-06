using System;
using UnityEngine;

namespace FireSafety
{
    [Serializable] public class TheoryLesson { public string title,body,narration; }
    [Serializable] public class TheoryQuestion { public string prompt,explanation,narration; public string[] answers; public int correctAnswer; }
    [Serializable] public class TheoryCourse { public TheoryLesson[] pages; public TheoryQuestion[] questions; }
    public static class TrainingTheory
    {
        static TheoryCourse course;
        public static TheoryCourse Course
        {
            get
            {
                if(course==null)
                {
                    var asset=Resources.Load<TextAsset>("FireSafetyTheory");
                    if(asset==null)throw new InvalidOperationException("Missing FireSafetyTheory lesson content.");
                    course=JsonUtility.FromJson<TheoryCourse>(asset.text);
                }
                return course;
            }
        }
    }
}
