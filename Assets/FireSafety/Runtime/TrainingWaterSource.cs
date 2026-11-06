using UnityEngine;

namespace FireSafety
{
    public class TrainingWaterSource : MonoBehaviour
    {
        public TrainingSession session;
        public Transform spout;
        public ParticleSystem stream;
        public string sourceName="Washroom tap";
        public float litersPerSecond=1.2f;
        public bool Flowing {get;private set;}
        public void ResetSource(){Flowing=false;if(stream!=null)stream.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
        public void Toggle()
        {
            if(session.phase!=SessionPhase.Assessing)return;
            Flowing=!Flowing;
            if(stream!=null){if(Flowing)stream.Play();else stream.Stop();}
            session.Respond();session.Record(Flowing?"Water outlet opened":"Water outlet closed",sourceName,"observed");
        }
        void Update()
        {
            if(!Flowing || session.phase!=SessionPhase.Assessing || session.player.TrackingUnavailable)return;
            var bucket=session.player.HeldBucket;
            if(bucket!=null)bucket.FillFrom(this,Time.deltaTime);
        }
    }
}
