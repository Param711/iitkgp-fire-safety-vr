using UnityEngine;

namespace FireSafety
{
    public class TrainingHazard : MonoBehaviour
    {
        public TrainingSession session;
        public bool isRouteFire;
        public ParticleSystem flames;
        public ParticleSystem smoke;
        public Light glow;
        public AudioSource fireAudio;
        public float intensity=1;
        public float footprintScale=1;
        public bool Active { get; private set; }
        public bool Extinguished { get; private set; }
        public FireKind kind;
        public GameObject[] sourceProps;
        public float strength=8;
        readonly ParticleSystem.Particle[] smokeParticles=new ParticleSystem.Particle[80];
        public bool PortableSuppressionSuitable {get{return !isRouteFire&&footprintScale<=1.45f&&strength<=12;}}
        public void ResetHazard(FireKind fire,bool active,float initialStrength=8,float initialFootprint=1)
        {
            kind=fire;Active=active;Extinguished=false;strength=initialStrength;footprintScale=initialFootprint;intensity=Mathf.Clamp(strength/8,.25f,2.3f);
            if(sourceProps!=null)for(int i=0;i<sourceProps.Length;i++)sourceProps[i].SetActive(i==(int)fire);
            if(flames!=null)
            {
                var emission=flames.emission;emission.rateOverTime=footprintScale>1?105:42;
                var shape=flames.shape;shape.radius=footprintScale>1?1.85f:.22f;
                var main=flames.main;main.maxParticles=footprintScale>1?130:65;
                main.startSize3D=true;main.startSizeX=.45f*footprintScale;main.startSizeY=.95f*footprintScale;main.startSizeZ=.45f*footprintScale;
                main.startLifetime=footprintScale>1?1.35f:.85f;main.startSpeed=footprintScale>1?1.8f:1.2f;
            }
            ConfigureSmoke();
            if(flames!=null){flames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(active)flames.Play();}
            if(smoke!=null){smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(active){smoke.Play();smoke.Emit(footprintScale>1.45f?18:8);}}
            if(glow!=null)glow.enabled=active;
        }
        public void Grow(float dt)
        {
            if(!Active)return;
            footprintScale=Mathf.Min(1.8f,footprintScale+Mathf.Max(0,dt)*.012f);
            strength=Mathf.Min(24,strength+Mathf.Max(0,dt)*.08f);intensity=Mathf.Clamp(strength/8,.25f,2.3f);
            if(flames!=null){var shape=flames.shape;shape.radius=.22f+Mathf.Max(0,footprintScale-1)*1.6f;var emission=flames.emission;emission.rateOverTime=42*footprintScale;var main=flames.main;main.maxParticles=96;main.startSizeX=.45f*footprintScale;main.startSizeY=.95f*footprintScale;main.startSizeZ=.45f*footprintScale;}
            ConfigureSmoke();
        }
        void ConfigureSmoke()
        {
            if(smoke==null)return;
            float severity=Mathf.Clamp01((footprintScale-.8f)/1.0f);
            var main=smoke.main;main.startSize=Mathf.Lerp(1.0f,1.45f,severity);main.startLifetime=5.5f;
            main.startSpeed=.42f;main.maxParticles=80;
            main.startColor=Color.Lerp(new Color(.46f,.47f,.48f,.82f),new Color(.25f,.27f,.29f,.92f),severity);
            var shape=smoke.shape;shape.radius=Mathf.Lerp(.28f,.65f,severity);
            var emission=smoke.emission;emission.rateOverTime=Mathf.Lerp(15,25,severity);
        }
        void LateUpdate(){ConfineSmokeToFloor();}
        public void ConfineSmokeToFloor()
        {
            if(smoke==null||!smoke.IsAlive())return;
            // Keep billboards beneath this floor's ceiling; reusing the buffer avoids per-frame allocation.
            float ceiling=Mathf.Floor((transform.position.y+.2f)/3.5f)*3.5f+3.25f;
            int count=smoke.GetParticles(smokeParticles);
            for(int i=0;i<count;i++)
            {
                var particle=smokeParticles[i];float top=ceiling-particle.GetCurrentSize(smoke)*.71f;
                if(particle.position.y<=top)continue;
                var p=particle.position;p.y=top;particle.position=p;
                var velocity=particle.velocity;velocity.y=Mathf.Min(0,velocity.y);particle.velocity=velocity;
                smokeParticles[i]=particle;
            }
            smoke.SetParticles(smokeParticles,count);
        }
        void Update()
        {
            if(glow!=null && Active)glow.intensity=(0.5f+Mathf.PerlinNoise(Time.time*6,transform.position.z)*0.7f)*intensity;
            if(fireAudio!=null)
            {
                bool sound=Active&&session.phase==SessionPhase.Assessing&&!session.player.TrackingUnavailable;
                if(sound&&!fireAudio.isPlaying)fireAudio.Play();else if(!sound&&fireAudio.isPlaying)fireAudio.Stop();
                fireAudio.volume=.25f+.2f*intensity;
            }
        }
        public float HeatAt(Vector3 p)
        { return Active && Mathf.Abs(p.y-transform.position.y)<2.2f && Vector2.Distance(new Vector2(p.x,p.z),new Vector2(transform.position.x,transform.position.z))<(isRouteFire?1.3f:1.0f)*footprintScale?1:0; }
        public float SmokeAt(Vector3 head)
        { return Active && head.y>transform.position.y+0.9f && head.y<transform.position.y+3.1f && Vector2.Distance(new Vector2(head.x,head.z),new Vector2(transform.position.x,transform.position.z))<(isRouteFire?1.7f:2.5f)*footprintScale?1:0; }
        public void Suppress(float dt)
        {
            if(!Active || !PortableSuppressionSuitable)return;
            strength=Mathf.Max(0,strength-dt);intensity=Mathf.Clamp01(strength/8);
            if(flames!=null){var e=flames.emission;e.rateOverTime=12+38*intensity;}
            if(strength>0)return;
            Extinguished=true;Active=false;
            if(flames!=null)flames.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            if(smoke!=null)smoke.Stop(true,ParticleSystemStopBehavior.StopEmitting);
            if(glow!=null)glow.enabled=false;
            if(session!=null){session.report.extinguishedSeconds=session.Elapsed;session.Record("Incident suppressed","Controlled incipient fire extinguished","observed");}
        }
    }
}
