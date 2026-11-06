using System;
using UnityEngine;

namespace FireSafety
{
    [Serializable] public class TrainingGameSettings
    {
        public int difficulty=1, intensity=1, location, floor=2;
        public FireKind fireKind;
        public static readonly string[] Difficulties={"EASY","MEDIUM","HARD"};
        public static readonly string[] Intensities={"SMALL","GROWING","LARGE"};
        public ScenarioDefinition CreateScenario(string id)
        {
            int level=Mathf.Clamp(difficulty,0,2),size=Mathf.Clamp(intensity,0,2),site=Mathf.Clamp(location,0,IncidentSites.Names.Length-1);
            int storey=IncidentSites.GroundOnly(site)?0:Mathf.Clamp(floor,0,2);
            return new ScenarioDefinition {id=id,title=Difficulties[level]+" / "+Intensities[size]+" / "+IncidentSites.Names[site]+" / "+IncidentSites.FloorName(storey),
                fireKind=fireKind,startFloor=storey,incidentRoom=site,initialFootprint=size==2?1.8f:size==1?1.2f:.8f,
                initialStrength=size==2?28:size==1?11:6,developingFire=size>0,
                escalationSeconds=(level==0?110:level==1?75:45)*(size==2?.8f:1),
                durationLimit=level==0?480:level==1?420:300,primaryBlocked=site==6,exitBlocked=site==7,secondaryExitBlocked=site==10};
        }
        // Each draw samples every setting independently; this is not the preset scenario bag.
        public static TrainingGameSettings Draw(System.Random random)
        {return new TrainingGameSettings {difficulty=random.Next(3),intensity=random.Next(3),fireKind=(FireKind)random.Next(3),location=random.Next(IncidentSites.Names.Length),floor=random.Next(3)};}
    }
    public static class IncidentSites
    {
        public static readonly string[] Names={"Classroom","Staff office","Computer laboratory","Main corridor","Water-cooler area","Lift lobby","Main staircase","Main entrance","Male washroom","Female washroom","Second ground-floor exit"};
        public static string FloorName(int floor){return floor==0?"Ground floor":"Floor "+floor;}
        public static bool IsEnclosed(int site){return site<3||site==8||site==9;}
        public static bool GroundOnly(int site){return site==7||site==10;}
        public static Vector3 Position(int site,int floor)
        {
            Vector3[] sites={new Vector3(5.8f,0,5.15f),new Vector3(-3.2f,0,10),new Vector3(3.2f,0,22),new Vector3(.7f,0,17),
                new Vector3(1.05f,0,-2),new Vector3(-.4f,0,-2),new Vector3(2.6f,0,13),new Vector3(-4.2f,0,13),new Vector3(3.3f,0,27),new Vector3(0,0,32.3f),new Vector3(-4.2f,0,.85f)};
            var p=sites[Mathf.Clamp(site,0,sites.Length-1)];p.y=(GroundOnly(site)?0:floor*3.5f)+.08f;return p;
        }
        public static Vector3 ObserverPosition(int site,int floor)
        {
            var p=Position(site,floor);p.y=floor*3.5f+.06f;
            if(site<3||site==8){p.x=.1f;p.z+=.6f;}
            else if(site==7){p.x=.1f;p.z=13.4f;}
            else if(site==9){p.z=28.5f;p.x=0;}
            else if(site==10){p.z=.85f;p.x=0;}
            else {p.x=0;p.z+=3.5f;}
            if(site==0)p.z=4.6f;
            return p;
        }
    }
}
