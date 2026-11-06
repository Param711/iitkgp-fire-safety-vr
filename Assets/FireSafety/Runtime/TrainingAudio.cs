using UnityEngine;

namespace FireSafety
{
    public class TrainingAudio : MonoBehaviour
    {
        public TrainingSession session;
        public AudioSource narration;
        public AudioClip[] fireBriefings;
        public AudioClip desktopControls,vrControls,equipment,soundCheck,alarmAcknowledged,callAcknowledged;
        public AudioClip[] fireVoices,alarmVoices,waitingVoices;
        public AudioClip[] theoryLessons,theoryQuestions,theoryAnswers;
        float nextOccupantVoice;
        bool suspended;
        public void Play(AudioClip clip)
        {
            if(narration==null||clip==null)return;
            narration.Stop();narration.clip=clip;narration.loop=false;narration.Play();
        }
        public void Brief(FireKind kind){Play(fireBriefings[(int)kind]);}
        public void Controls(bool vr){Play(vr?vrControls:desktopControls);}
        public void BeginAssessment(){if(narration!=null)narration.Stop();nextOccupantVoice=Time.time+1;}
        public void Stop(){if(narration!=null)narration.Stop();}
        public void Occupant(TrainingNPC occupant,bool arrived=false)
        {
            if(session.phase!=SessionPhase.Assessing||session.player.TrackingUnavailable||Time.time<nextOccupantVoice||occupant.voice==null)return;
            if(Mathf.Abs(occupant.transform.position.y-session.player.Feet.y)>2||Vector3.Distance(occupant.transform.position,session.player.Head.position)>14)return;
            var clips=arrived?waitingVoices:session.AlarmRaised?alarmVoices:fireVoices;
            if(clips==null||clips.Length==0)return;
            occupant.voice.clip=clips[occupant.identity%clips.Length];occupant.voice.Play();nextOccupantVoice=Time.time+5;
        }
        void Update()
        {
            bool pause=session.player.TrackingUnavailable;
            if(pause==suspended)return;
            suspended=pause;
            if(narration!=null){if(pause)narration.Pause();else narration.UnPause();}
            if(session.alarmAudio!=null){if(pause)session.alarmAudio.Pause();else session.alarmAudio.UnPause();}
            foreach(var occupant in session.occupants)if(occupant.voice!=null){if(pause)occupant.voice.Pause();else occupant.voice.UnPause();}
        }
    }
}
