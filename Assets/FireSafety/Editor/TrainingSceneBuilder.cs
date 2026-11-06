using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FireSafety.Editor
{
    public static class TrainingSceneBuilder
    {
        public const string ScenePath="Assets/Scenes/FireSafetyTraining.unity";
        static TrainingSession session;
        static TrainingRouteGraph graph;
        static Transform root;
        static Material cream,tile,trim,wood,metal,navy,teal,red,white,glass,asphalt,grass,skin;
        static readonly List<TrainingDoor> allDoors=new List<TrainingDoor>();
        static readonly List<TrainingExtinguisher> allTools=new List<TrainingExtinguisher>();
        static readonly List<TrainingBucket> allBuckets=new List<TrainingBucket>();
        static readonly List<TrainingWaterSource> allWaterSources=new List<TrainingWaterSource>();
        static readonly List<TrainingHazard> routeFires=new List<TrainingHazard>();
        static readonly List<TrainingNPC> npcs=new List<TrainingNPC>();
        static readonly List<int[]> floorNodes=new List<int[]>();
        static readonly List<int> refugeNodes=new List<int>();
        static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
        static readonly float[] corridorZ={-2,2,6,10,13,18,22.5f,27,29.4f};

        [MenuItem("Fire Safety/Build Complete Assessment Environment")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before rebuilding.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory("Assets/FireSafety/Generated/Materials");AssetDatabase.Refresh();
            allDoors.Clear();allTools.Clear();allBuckets.Clear();allWaterSources.Clear();routeFires.Clear();npcs.Clear();floorNodes.Clear();refugeNodes.Clear();mats.Clear();
            root=new GameObject("Department Fire Safety Assessment").transform;
            session=root.gameObject.AddComponent<TrainingSession>();graph=root.gameObject.AddComponent<TrainingRouteGraph>();session.graph=graph;
            Materials();Lighting();
            session.spawnPoints=new Transform[3];session.refugeDoors=new TrainingDoor[3];
            session.incidentDoors=new TrainingDoor[3];
            var leftDoors=new Transform[3];var rightDoors=new Transform[3];
            for(int f=0;f<3;f++)BuildFloor(f,leftDoors,rightDoors);
            BuildStairs(13,RouteKind.PrimaryStairs);
            BuildExterior();BuildLift(leftDoors,rightDoors);BuildPlayer();BuildPresentation();BuildOccupants();
            session.incident=Hazard("Office incident",new Vector3(3.2f,7.08f,4),false);
            BuildFireSources(session.incident);
            session.fireDevelopment=root.gameObject.AddComponent<TrainingFireDevelopment>();session.fireDevelopment.session=session;
            session.fireDevelopment.zones=new[]{Hazard("Room growth zone",Vector3.zero,true),Hazard("Corridor spread zone",Vector3.zero,true),Hazard("Other room spread zone",Vector3.zero,true)};
            session.fireDevelopment.Reset();
            session.routeFires=routeFires.ToArray();session.extinguishers=allTools.ToArray();session.doors=allDoors.ToArray();session.occupants=npcs.ToArray();
            session.buckets=allBuckets.ToArray();session.waterSources=allWaterSources.ToArray();
            session.alarmAudio=root.gameObject.AddComponent<AudioSource>();session.alarmAudio.playOnAwake=false;session.alarmAudio.spatialBlend=0;
            session.alarmAudio.clip=Clip("TrainingAlarm");
            BuildAudio();
            ConfigureRendering();MarkStaticGeometry();
            ConfigureLayers();
            foreach(var h in routeFires)h.ResetHazard(FireKind.EnergizedElectrical,false);
            session.incident.ResetHazard(FireKind.OrdinaryCombustibles,false);
            EditorSceneManager.SaveScene(scene,ScenePath);
            var existing=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            existing.RemoveAll(s=>s.path==ScenePath);existing.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=existing.ToArray();
            PlayerSettings.companyName="DepartmentTraining";PlayerSettings.productName="Department Fire Safety";
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
            AssetDatabase.SaveAssets();
            Debug.Log("FIRE_SAFETY_BUILD_OK: "+allDoors.Count+" doors; "+allTools.Count+" tools; "+npcs.Count+" NPCs; "+graph.nodes.Count+" route nodes.");
        }
        static Material Mat(string name,Color color,bool unlit=false)
        {
            if(mats.TryGetValue(name,out var found))return found;
            string path="Assets/FireSafety/Generated/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetColor("_BaseColor",color);m.enableInstancing=true;
            if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",name=="Tile"?.35f:.12f);
            mats[name]=m;return m;
        }
        static void Materials()
        {
            cream=Mat("Cream",new Color(.84f,.82f,.72f));tile=Mat("Tile",new Color(.76f,.78f,.75f));trim=Mat("Dark trim",new Color(.13f,.17f,.18f));
            wood=Mat("Warm wood",new Color(.35f,.19f,.10f));metal=Mat("Brushed metal",new Color(.52f,.58f,.60f));navy=Mat("Navy",new Color(.035f,.09f,.14f));
            teal=Mat("Safety green",new Color(.025f,.40f,.31f));red=Mat("Equipment red",new Color(.72f,.055f,.04f));white=Mat("Ivory",new Color(.91f,.93f,.89f));
            glass=Mat("Blue window",new Color(.37f,.63f,.75f));asphalt=Mat("Asphalt",new Color(.16f,.20f,.22f));grass=Mat("Grass",new Color(.23f,.36f,.22f));skin=Mat("Skin",new Color(.62f,.39f,.25f));
        }
        static Transform Group(string name,Transform parent)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool collision=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        static GameObject Shape(string name,PrimitiveType shape,Transform parent,Vector3 p,Vector3 scale,Material mat,bool collision=false)
        {
            var g=GameObject.CreatePrimitive(shape);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collision)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static TextMesh Text(string name,Transform parent,Vector3 p,string text,float size,Color color,Quaternion rotation,TextAnchor anchor=TextAnchor.MiddleCenter)
        {
            var g=Group(name,parent);g.localPosition=p;g.localRotation=rotation;var tm=g.gameObject.AddComponent<TextMesh>();tm.text=text;tm.fontSize=48;tm.characterSize=size;tm.anchor=anchor;tm.alignment=anchor==TextAnchor.UpperLeft?TextAlignment.Left:TextAlignment.Center;tm.color=color;
            tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string path="Assets/FireSafety/Generated/Materials/WorldText.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("FireSafety/WorldText"));AssetDatabase.CreateAsset(material,path);}
            g.GetComponent<MeshRenderer>().sharedMaterial=material;g.gameObject.AddComponent<TrainingWorldText>();
            return tm;
        }
        static void Sign(Transform parent,Vector3 p,string text,float width=1.3f,Quaternion? rotation=null,Material material=null)
        {
            var group=Group("Sign "+text,parent);group.localPosition=p;group.localRotation=rotation??Quaternion.identity;
            Box("Sign plate",group,Vector3.zero,new Vector3(width,.32f,.04f),material??teal,false);
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");font.RequestCharactersInTexture(text,48,FontStyle.Normal);
            float longest=0;foreach(var line in text.Split('\n'))
            {
                float advance=0;foreach(char c in line)if(font.GetCharacterInfo(c,out CharacterInfo info,48,FontStyle.Normal))advance+=info.advance;
                longest=Mathf.Max(longest,advance);
            }
            float letterSize=Mathf.Min(.035f,width*.88f*10/Mathf.Max(1,longest));
            Text("Legend",group,new Vector3(0,0,-.025f),text,letterSize,Color.white,Quaternion.identity);
        }
        static void Lighting()
        {
            var sun=Group("Daylight",root).gameObject.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.05f;sun.color=new Color(1,.94f,.82f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(45,-25,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.70f,.73f);
            RenderSettings.fog=false;
        }
        static void BuildFloor(int f,Transform[] left,Transform[] right)
        {
            float y=f*3.5f;var floor=Group(f==0?"Ground floor":"Floor "+f,root);floor.position=Vector3.up*y;
            Box("Corridor floor",floor,new Vector3(0,-.08f,15),new Vector3(3,.16f,30),tile);
            Box("Corridor ceiling",floor,new Vector3(0,3.48f,15),new Vector3(3,.12f,30),white);
            // Ground-floor office spacing leaves the confirmed four-metre entrance clear.
            float[] roomZ={4,f==0?8.8f:10,17.5f,22.5f};
            WallWithOpenings(floor,-1.6f,f==0?new[]{.85f,4f,8.8f,13f,17.5f,22.5f}:roomZ,30,f==0?4:1.3f);
            WallWithOpenings(floor,1.6f,new[]{4f,22f,13f,14.8f,27f},30);
            for(int r=0;r<roomZ.Length;r++)
            {
                bool refuge=r==3;
                var d=BuildRoom(floor,f,r,roomZ[r],false,refuge);
                if(refuge)session.refugeDoors[f]=d;
            }
            session.incidentDoors[f]=BuildRoom(floor,f,4,4,true,false);BuildRoom(floor,f,5,22,true,false);
            BuildRoom(floor,f,6,27,true,false);BuildFemaleWashroom(floor,f);
            for(float z=1.5f;z<30;z+=3)
            {
                Box("Tile grout",floor,new Vector3(0,.003f,z),new Vector3(3,.004f,.012f),metal,false);
                Box("Ceiling fixture",floor,new Vector3(0,3.35f,z),new Vector3(.28f,.05f,1.1f),metal,false);
                Box("Light diffuser",floor,new Vector3(0,3.31f,z),new Vector3(.22f,.02f,1),Mat("Light white",new Color(.95f,1,1),true),false);
            }
            Box("Skirting left",floor,new Vector3(-1.48f,.08f,15),new Vector3(.03f,.16f,30),trim,false);
            // Benches leave the corridor center and stair approaches clear.
            for(int b=0;b<2;b++)
            {
                float z=b==0?7:24.5f;
                Box("Corridor bench seat",floor,new Vector3(-1.1f,.43f,z),new Vector3(.55f,.09f,1.6f),wood);
                Box("Bench back",floor,new Vector3(-1.35f,.75f,z),new Vector3(.07f,.55f,1.6f),wood);
                for(int a=-1;a<=1;a+=2)Box("Bench leg",floor,new Vector3(-1.1f,.2f,z+a*.6f),new Vector3(.4f,.4f,.07f),trim);
            }
            BuildLobby(floor,f,left,right);
            Sign(floor,new Vector3(0,2.7f,12.05f),"MAIN STAIRS  >",1.7f);
            Sign(floor,new Vector3(0,2.7f,26.05f),"FEMALE WASHROOM  ^\nMALE WASHROOM  >",2.1f);
            Sign(floor,new Vector3(0,2.6f,3),"WASHROOMS STRAIGHT AHEAD\nWOMEN: END / MEN: RIGHT",2.4f);
            Sign(floor,new Vector3(0,2.6f,20),"WASHROOMS AHEAD\nWOMEN STRAIGHT / MEN RIGHT",2.4f);
            if(f==0)Sign(floor,new Vector3(-1.4f,2.6f,13),"MAIN EXIT",1.5f,Quaternion.Euler(0,-90,0));
            else Sign(floor,new Vector3(0,2.7f,.1f),"GROUND VIA STAIRS",2.0f,Quaternion.Euler(0,180,0));
            var spawn=Group("Assessment start",floor);spawn.localPosition=new Vector3(.15f,.06f,4.6f);spawn.rotation=Quaternion.Euler(0,100,0);session.spawnPoints[f]=spawn;
            Equipment(floor,new Vector3(1.33f,1.25f,6.7f),"FIRE ALARM",InteractionAction.Alarm,red);
            Equipment(floor,new Vector3(1.33f,1.25f,23.7f),"FIRE ALARM",InteractionAction.Alarm,red);
            Equipment(floor,new Vector3(-1.33f,1.25f,f==0?2.0f:1.2f),"EMERGENCY CALL",InteractionAction.Assistance,teal);
            Tool(floor,new Vector3(1.24f,.8f,7.5f),AgentKind.DryChemicalABC);
            var nodes=new int[corridorZ.Length];
            for(int i=0;i<corridorZ.Length;i++){nodes[i]=graph.Add("F"+f+" corridor "+i,new Vector3(0,y,corridorZ[i]));if(i>0)graph.Link(nodes[i-1],nodes[i]);}
            floorNodes.Add(nodes);
            int rn=graph.Add("F"+f+" refuge",new Vector3(-4.1f,y,22.5f),RouteKind.Refuge);refugeNodes.Add(rn);graph.Link(nodes[6],rn);
        }
        static void WallWithOpenings(Transform floor,float x,float[] centers,float length,float entranceWidth=1.3f)
        {
            Array.Sort(centers);float start=0;
            foreach(float c in centers)
            {
                float width=Mathf.Approximately(c,13)?entranceWidth:1.3f;
                float a=Mathf.Max(start,c-width/2);if(a>start)Box("Corridor wall",floor,new Vector3(x,1.7f,(start+a)/2),new Vector3(.2f,3.4f,a-start),cream);
                Box("Door header",floor,new Vector3(x,2.9f,c),new Vector3(.2f,1,width),cream);start=c+width/2;
            }
            if(start<length)Box("Corridor wall",floor,new Vector3(x,1.7f,(start+length)/2),new Vector3(.2f,3.4f,length-start),cream);
        }
        static TrainingDoor BuildRoom(Transform floor,int f,int index,float z,bool east,bool refuge)
        {
            float sign=east?1:-1;float x=sign*4.2f;float w=5;float depth=3.9f;
            bool washroom=east&&index==6;
            string roomName=refuge?"OFFICE "+f+"04":washroom?"MALE WASHROOM "+f:east&&index==4?"CLASSROOM "+f+"05":!east&&index==1?"STAFF OFFICE "+f+"02":"ROOM "+f+"0"+(index+1);
            var room=Group(roomName,floor);
            Box("Room floor",room,new Vector3(x,-.08f,z),new Vector3(w,.16f,depth),tile);
            Box("Door threshold",room,new Vector3(sign*1.6f,-.08f,z),new Vector3(.3f,.16f,1.3f),tile);
            Box("Room ceiling",room,new Vector3(x,3.42f,z),new Vector3(w,.12f,depth),white);
            Box("Exterior wall",room,new Vector3(sign*6.8f,1.7f,z),new Vector3(.2f,3.4f,depth),cream);
            for(int dir=-1;dir<=1;dir+=2)Box("Room partition",room,new Vector3(x,1.7f,z+dir*(depth/2+.05f)),new Vector3(w,3.4f,.1f),cream);
            Box("Room window",room,new Vector3(sign*6.68f,1.9f,z),new Vector3(.03f,1.3f,2.2f),glass,false);
            Box("Window mullion",room,new Vector3(sign*6.63f,1.9f,z),new Vector3(.06f,1.4f,.06f),white,false);
            var doorway=Group("Door "+roomName,room);doorway.localPosition=new Vector3(sign*1.58f,0,z-.56f);doorway.localRotation=Quaternion.Euler(0,90,0);
            var door=doorway.gameObject.AddComponent<TrainingDoor>();door.session=session;door.roomName=roomName;door.refugeDoor=refuge;door.openAngle=east?100:-100;
            var pivot=Group("Hinge",doorway);door.leaf=pivot;
            var leaf=Box("Door leaf",pivot,new Vector3(-.56f,1.12f,0),new Vector3(1.12f,2.24f,.055f),wood);
            var interact=leaf.AddComponent<TrainingInteractable>();interact.session=session;interact.action=InteractionAction.Door;interact.door=door;interact.objectLabel="Door";
            Shape("Door handle",PrimitiveType.Sphere,pivot,new Vector3(-.95f,1,-.08f),Vector3.one*.08f,metal);
            allDoors.Add(door);
            Sign(floor,new Vector3(sign*1.45f,2.55f,z),roomName,1.3f,Quaternion.Euler(0,east?90:-90,0),navy);
            if(refuge)
            {
                Box("Office desk",room,new Vector3(-4.5f,.76f,z+.9f),new Vector3(1.5f,.09f,.7f),wood);
                Box("Desk pedestal",room,new Vector3(-5,.37f,z+.9f),new Vector3(.35f,.74f,.6f),cream);
                OfficePhone(room,new Vector3(-4.4f,.86f,z+.9f));
                Box("Office visitor bench",room,new Vector3(-6.25f,.45f,z),new Vector3(.45f,.2f,1.6f),wood);
            }
            else if(washroom)
            {
                BuildWashroom(room,f,new Vector3(4.2f,0,z),false);
            }
            else if(east&&index==4)
            {
                BuildClassroom(room,z);
            }
            else if(east&&index==5)
            {
                BuildComputerLab(room,x,z);
            }
            else
            {
                // Three repeatable furniture variants can be replaced with photo-matched templates.
                int style=index%3;
                for(int desk=0;desk<(style==1?2:1);desk++)
                {
                    float dz=z+(desk==0?-.85f:.85f);
                    Box("Desk top",room,new Vector3(x+sign*.55f,.77f,dz),new Vector3(1.5f,.08f,.75f),wood);
                    for(int lx=-1;lx<=1;lx+=2)for(int lz=-1;lz<=1;lz+=2)Box("Desk leg",room,new Vector3(x+sign*.55f+lx*.64f,.37f,dz+lz*.26f),new Vector3(.05f,.74f,.05f),metal);
                    Box("Monitor",room,new Vector3(x+sign*.6f,1.12f,dz+.15f),new Vector3(.58f,.38f,.055f),navy,false);
                    Box("Screen",room,new Vector3(x+sign*.6f,1.12f,dz+.117f),new Vector3(.52f,.31f,.008f),Mat("Screen blue",new Color(.12f,.36f,.44f),true),false);
                    Box("Keyboard",room,new Vector3(x+sign*.6f,.83f,dz-.17f),new Vector3(.44f,.025f,.14f),trim,false);
                    Box("Chair",room,new Vector3(x-sign*.35f,.45f,dz),new Vector3(.45f,.1f,.45f),navy,false);
                    Box("Chair back",room,new Vector3(x-sign*.55f,.75f,dz),new Vector3(.08f,.6f,.45f),navy,false);
                }
                Box("Storage cabinet",room,new Vector3(x+sign*1.8f,1,z+1.2f),new Vector3(.5f,2,.8f),cream);
                for(int s=0;s<4;s++)Box("Cabinet shelf",room,new Vector3(x+sign*1.5f,.3f+s*.45f,z+1.2f),new Vector3(.1f,.05f,.75f),wood,false);
            }
            Box("Room ceiling light",room,new Vector3(x,3.3f,z),new Vector3(.3f,.04f,1.1f),Mat("Light white",Color.white,true),false);
            return door;
        }
        static void Equipment(Transform parent,Vector3 p,string label,InteractionAction action,Material color)
        {
            // Panels face into the corridor when mounted on a side wall.
            var g=Group(label,parent);g.localPosition=p;
            float angle=Mathf.Abs(p.x)<1.5f?(p.x>0?90:-90):0;
            g.localRotation=Quaternion.Euler(0,angle,0);
            var casing=Box("Panel casing",g,Vector3.zero,new Vector3(.28f,.38f,.08f),color);
            var button=Box("Press",g,new Vector3(0,-.03f,-.065f),new Vector3(.18f,.18f,.06f),white);
            var i=button.AddComponent<TrainingInteractable>();i.session=session;i.action=action;i.objectLabel=label;
            var wholePanel=casing.AddComponent<TrainingInteractable>();wholePanel.session=session;wholePanel.action=action;wholePanel.objectLabel=label;
            if(action==InteractionAction.Alarm)Text("Press label",g,new Vector3(0,-.03f,-.101f),"PRESS",.010f,Color.black,Quaternion.identity);
            Text("Panel legend",g,new Vector3(0,.28f,-.06f),label,.018f,Color.white,Quaternion.identity);
        }
        static void BuildLobby(Transform floor,int f,Transform[] left,Transform[] right)
        {
            Box("Lobby floor",floor,new Vector3(0,-.08f,-2),new Vector3(3.4f,.16f,4),tile);
            Box("Lobby ceiling",floor,new Vector3(0,3.48f,-2),new Vector3(3.4f,.12f,4),white);
            Box("Lobby right wall",floor,new Vector3(1.8f,1.7f,-2),new Vector3(.2f,3.4f,4),cream);
            Box("Lobby back wall",floor,new Vector3(0,1.7f,-4.1f),new Vector3(3.6f,3.4f,.2f),cream);
            for(int side=-1;side<=1;side+=2)Box("Lift wall",floor,new Vector3(-1.8f,1.7f,-2+side*1.65f),new Vector3(.2f,3.4f,.7f),cream);
            Box("Lift lintel",floor,new Vector3(-1.8f,2.9f,-2),new Vector3(.2f,1,2.6f),cream);
            left[f]=Box("Landing left door",floor,new Vector3(-1.78f,1.1f,-2.39f),new Vector3(.07f,2.2f,.78f),metal).transform;
            right[f]=Box("Landing right door",floor,new Vector3(-1.78f,1.1f,-1.61f),new Vector3(.07f,2.2f,.78f),metal).transform;
            var call=Box("Elevator call",floor,new Vector3(-1.64f,1.2f,-.9f),new Vector3(.08f,.2f,.15f),white);
            var i=call.AddComponent<TrainingInteractable>();i.session=session;i.action=InteractionAction.LiftFloor;i.value=f;
            Sign(floor,new Vector3(-1.55f,2.45f,-2),"LIFT / "+f,1.3f,Quaternion.Euler(0,-90,0),navy);
            Sign(floor,new Vector3(1.67f,2.65f,-2),"IN FIRE\nUSE STAIRS",1.5f,Quaternion.Euler(0,90,0),red);
            BuildWaterCooler(floor,f);
        }
        static void Tool(Transform floor,Vector3 p,AgentKind agent)
        {
            var g=Group(agent+" extinguisher",floor);g.localPosition=p;
            var e=g.gameObject.AddComponent<TrainingExtinguisher>();e.session=session;e.agent=agent;
            var body=Shape("Cylinder",PrimitiveType.Cylinder,g,Vector3.zero,new Vector3(.19f,.28f,.19f),red,true);
            var i=body.AddComponent<TrainingInteractable>();i.session=session;i.action=InteractionAction.GrabExtinguisher;i.tool=e;
            Shape("Valve",PrimitiveType.Cylinder,g,new Vector3(0,.31f,0),new Vector3(.06f,.05f,.06f),metal);
            Box("Lever",g,new Vector3(0,.37f,0),new Vector3(.18f,.035f,.05f),trim,false);
            e.pin=Shape("Safety pin",PrimitiveType.Cylinder,g,new Vector3(.09f,.32f,0),new Vector3(.025f,.06f,.025f),Mat("Pin yellow",Color.yellow));
            var nozzle=Group("Nozzle",g);nozzle.localPosition=new Vector3(.12f,.35f,0);e.nozzle=nozzle;
            var tube=Shape("Nozzle tube",PrimitiveType.Cylinder,nozzle,new Vector3(0,0,.06f),new Vector3(.045f,.10f,.045f),trim);tube.transform.localRotation=Quaternion.Euler(90,0,0);
            e.discharge=Particles("Discharge",nozzle,new Vector3(0,0,.16f),false,true);
            e.sprayAudio=Audio(nozzle,"Nozzle spray sound",Clip("ExtinguisherSpray"),true,true,.45f);
            string label="FIRE EXTINGUISHER";
            Sign(floor,p+new Vector3(0,1.4f,0),label,1.3f,Quaternion.Euler(0,90,0),navy);
            allTools.Add(e);
        }
        static void BuildWaterCooler(Transform floor,int f)
        {
            var cooler=Group("Water cooler opposite lift",floor);
            Box("Stainless cooler cabinet",cooler,new Vector3(1.4f,.65f,-2),new Vector3(.5f,1.2f,.72f),metal);
            Box("Cooler drip tray",cooler,new Vector3(1.04f,.87f,-2),new Vector3(.24f,.045f,.5f),trim);
            Box("Cooler top",cooler,new Vector3(1.4f,1.28f,-2),new Vector3(.54f,.06f,.76f),metal);
            for(int i=0;i<6;i++)Box("Cooler ventilation grille",cooler,new Vector3(1.14f,.24f+i*.055f,-2),new Vector3(.015f,.018f,.46f),trim,false);
            WaterTap(cooler,new Vector3(1.04f,1.15f,-2),Quaternion.Euler(0,-90,0),"Water cooler, floor "+f,.2f);
            Sign(cooler,new Vector3(1.67f,1.8f,-2),"DRINKING WATER",1.0f,Quaternion.Euler(0,90,0),navy);
        }
        static void BuildWashroom(Transform room,int f,Vector3 center,bool female)
        {
            float z=center.z;
            var ceramic=Mat("Washroom ceramic",new Color(.90f,.92f,.89f));
            float sinkX=female?-.95f:6.05f;
            Box("Sink counter",room,new Vector3(sinkX,.82f,z-1.1f),new Vector3(.65f,.10f,.9f),ceramic);
            Box("Basin recess",room,new Vector3(sinkX,.88f,z-1.1f),new Vector3(.42f,.035f,.65f),metal,false);
            Box("Sink pedestal",room,new Vector3(sinkX,.41f,z-1.1f),new Vector3(.3f,.82f,.2f),ceramic);
            Box("Mirror",room,new Vector3(female?-1.46f:6.66f,1.6f,z-1.1f),new Vector3(.03f,.75f,1.1f),Mat("Mirror silver",new Color(.67f,.78f,.8f)),false);
            string name=(female?"Female":"Male")+" washroom, floor "+f;
            WaterTap(room,new Vector3(female?-1.25f:6.3f,1.09f,z-1.1f),Quaternion.Euler(0,female?90:-90,0),name+" tap",1.2f);
            for(int i=0;i<2;i++)
            {
                float x=center.x+(i==0?-.65f:.65f);
                Shape("Toilet pedestal",PrimitiveType.Capsule,room,new Vector3(x,.25f,z+1.12f),new Vector3(.4f,.25f,.5f),ceramic);
                Shape("Toilet seat",PrimitiveType.Cylinder,room,new Vector3(x,.5f,z+1.12f),new Vector3(.48f,.035f,.59f),ceramic);
                Box("Cistern",room,new Vector3(x,.73f,z+1.68f),new Vector3(.45f,.55f,.20f),ceramic);
                Box("Stall partition",room,new Vector3(x+.62f,1.1f,z+1.05f),new Vector3(.055f,2.2f,1.7f),white);
            }
            BuildBucket(room,new Vector3(female?.8f:2.8f,.035f,z+(female?-1.0f:.7f)),name);
        }
        static void BuildFemaleWashroom(Transform floor,int f)
        {
            var room=Group("FEMALE WASHROOM "+f,floor);
            Box("Female washroom floor",room,new Vector3(0,-.08f,32.2f),new Vector3(3,.16f,4.4f),tile);
            Box("Female washroom ceiling",room,new Vector3(0,3.45f,32.2f),new Vector3(3,.12f,4.4f),white);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Washroom side wall",room,new Vector3(side*1.6f,1.7f,32.2f),new Vector3(.2f,3.4f,4.4f),cream);
                Box("Washroom door jamb",room,new Vector3(side*1.125f,1.7f,30),new Vector3(.95f,3.4f,.18f),cream);
            }
            Box("Washroom back wall",room,new Vector3(0,1.7f,34.5f),new Vector3(3.4f,3.4f,.18f),cream);
            Box("Washroom door header",room,new Vector3(0,2.9f,30),new Vector3(1.3f,1,.18f),cream);
            var doorway=Group("Female washroom door",room);doorway.localPosition=new Vector3(-.56f,0,30);
            var door=doorway.gameObject.AddComponent<TrainingDoor>();door.session=session;door.roomName="FEMALE WASHROOM "+f;door.openAngle=-95;door.leaf=Group("Hinge",doorway);
            var leaf=Box("Door leaf",door.leaf,new Vector3(.56f,1.12f,0),new Vector3(1.12f,2.24f,.055f),wood);
            var interaction=leaf.AddComponent<TrainingInteractable>();interaction.session=session;interaction.action=InteractionAction.Door;interaction.door=door;allDoors.Add(door);
            Sign(room,new Vector3(0,2.55f,29.87f),"FEMALE WASHROOM",1.7f);
            BuildWashroom(room,f,new Vector3(0,0,32.2f),true);
        }
        static void OfficePhone(Transform parent,Vector3 p)
        {
            var phone=Group("Office telephone",parent);phone.localPosition=p;
            Box("Telephone base",phone,Vector3.zero,new Vector3(.28f,.07f,.35f),navy,false);
            var handset=Shape("Handset",PrimitiveType.Capsule,phone,new Vector3(0,.10f,0),new Vector3(.07f,.14f,.07f),trim);handset.transform.localRotation=Quaternion.Euler(0,0,90);
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)Box("Keypad",phone,new Vector3((col-1)*.05f,.04f,-.1f+row*.055f),new Vector3(.035f,.01f,.035f),white,false);
            var collider=phone.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.3f,.18f,.37f);collider.center=Vector3.up*.05f;
            var interaction=phone.gameObject.AddComponent<TrainingInteractable>();interaction.session=session;interaction.action=InteractionAction.Assistance;
        }
        static void BuildComputerLab(Transform room,float x,float z)
        {
            var blueBand=Mat("Lab blue band",new Color(.10f,.33f,.57f));
            Box("Blue lab wall band",room,new Vector3(6.67f,.83f,z),new Vector3(.025f,.5f,3.8f),blueBand,false);
            for(int row=0;row<2;row++)
            {
                float deskX=4.05f+row*1.95f;
                Box("Computer bench",room,new Vector3(deskX,.78f,z),new Vector3(.6f,.075f,3.2f),trim);
                for(int end=-1;end<=1;end+=2)Box("Bench support",room,new Vector3(deskX,.37f,z+end*1.4f),new Vector3(.48f,.74f,.045f),metal);
                for(int seat=0;seat<3;seat++)
                {
                    float computerZ=z+(seat-1)*1.0f;
                    Box("Lab monitor",room,new Vector3(deskX,1.1f,computerZ),new Vector3(.045f,.36f,.49f),trim,false);
                    Box("Lab screen",room,new Vector3(deskX-.026f,1.1f,computerZ),new Vector3(.006f,.30f,.43f),Mat("Lab screen",new Color(.16f,.30f,.37f),true),false);
                    Box("Monitor stand",room,new Vector3(deskX,.9f,computerZ),new Vector3(.045f,.2f,.05f),metal,false);
                    Box("Lab keyboard",room,new Vector3(deskX-.16f,.83f,computerZ),new Vector3(.2f,.025f,.38f),trim,false);
                    Box("Lab chair seat",room,new Vector3(deskX-.65f,.46f,computerZ),new Vector3(.4f,.08f,.44f),navy);
                    Box("Lab chair back",room,new Vector3(deskX-.85f,.72f,computerZ),new Vector3(.055f,.47f,.44f),navy);
                    for(int side=-1;side<=1;side+=2)Box("Chair leg",room,new Vector3(deskX-.65f,.23f,computerZ+side*.16f),new Vector3(.04f,.46f,.04f),metal,false);
                }
            }
        }
        static void BuildClassroom(Transform room,float z)
        {
            Box("Teaching board",room,new Vector3(4.5f,1.65f,z+1.86f),new Vector3(3,.95f,.045f),Mat("Class board",new Color(.08f,.22f,.18f)),false);
            Text("Lecture notes",room,new Vector3(4.5f,1.65f,z+1.83f),"INDUSTRIAL & SYSTEMS\nENGINEERING",.025f,Color.white,Quaternion.identity);
            for(int seat=0;seat<2;seat++)
            {
                float x=seat==0?4f:5.7f;
                Box("Student desk",room,new Vector3(x,.76f,z-.1f),new Vector3(1.1f,.08f,.6f),wood);
                for(int side=-1;side<=1;side+=2)Box("Student desk support",room,new Vector3(x+side*.5f,.36f,z-.1f),new Vector3(.06f,.72f,.4f),metal);
                Box("Student seat",room,new Vector3(x,.44f,z-.9f),new Vector3(.48f,.08f,.45f),navy,false);
                Box("Student seat back",room,new Vector3(x,.7f,z-1.13f),new Vector3(.48f,.5f,.06f),navy,false);
                Box("Open notebook",room,new Vector3(x,.81f,z-.15f),new Vector3(.35f,.015f,.23f),white,false);
            }
            Box("Teacher lectern",room,new Vector3(5.9f,.6f,z+.9f),new Vector3(.45f,1.2f,.45f),wood);
        }
        static TrainingWaterSource WaterTap(Transform parent,Vector3 p,Quaternion rotation,string name,float rate)
        {
            var g=Group(name,parent);g.localPosition=p;g.localRotation=rotation;
            Shape("Tap stem",PrimitiveType.Cylinder,g,new Vector3(0,-.07f,0),new Vector3(.055f,.09f,.055f),metal);
            Box("Tap spout",g,new Vector3(0,0,.09f),new Vector3(.055f,.045f,.22f),metal,false);
            Box("Tap handle",g,new Vector3(0,.055f,0),new Vector3(.16f,.025f,.055f),metal,false);
            var collider=g.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,0,.07f);collider.size=new Vector3(.2f,.16f,.27f);
            var source=g.gameObject.AddComponent<TrainingWaterSource>();source.session=session;source.sourceName=name;source.litersPerSecond=rate;
            var spout=Group("Water outlet",g);spout.localPosition=new Vector3(0,-.03f,.18f);spout.localRotation=Quaternion.Euler(90,0,0);source.spout=spout;
            source.stream=Particles("Running water",spout,Vector3.zero,false,true);
            var main=source.stream.main;main.startSpeed=.8f;main.startLifetime=.7f;main.startSize=.018f;main.maxParticles=24;main.startColor=new Color(.65f,.85f,1,.75f);
            var shape=source.stream.shape;shape.angle=1;shape.radius=.012f;
            var interaction=g.gameObject.AddComponent<TrainingInteractable>();interaction.session=session;interaction.action=InteractionAction.ToggleWater;interaction.waterSource=source;
            allWaterSources.Add(source);return source;
        }
        static void BuildBucket(Transform parent,Vector3 p,string location)
        {
            var g=Group("Refillable water bucket",parent);g.localPosition=p;
            var bucket=g.gameObject.AddComponent<TrainingBucket>();bucket.session=session;bucket.locationName=location;
            var visual=Group("Bucket body and handle",g);bucket.visual=visual;
            var filter=visual.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=BucketMesh();
            visual.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Mat("Bucket blue",new Color(.07f,.30f,.54f));
            for(int s=0;s<12;s++)
            {
                float a=s*Mathf.PI/12,b=(s+1)*Mathf.PI/12;
                var start=new Vector3(Mathf.Cos(a)*.17f,.33f+Mathf.Sin(a)*.19f,0);var end=new Vector3(Mathf.Cos(b)*.17f,.33f+Mathf.Sin(b)*.19f,0);
                var handle=Shape("Bucket handle",PrimitiveType.Cylinder,visual,(start+end)/2,new Vector3(.014f,Vector3.Distance(start,end)/2,.014f),metal);
                handle.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(end-start).normalized);
            }
            bucket.waterSurface=Shape("Bucket water level",PrimitiveType.Cylinder,visual,new Vector3(0,.035f,0),new Vector3(.2f,.003f,.2f),Mat("Bucket water",new Color(.24f,.58f,.72f))).transform;
            bucket.waterSurface.gameObject.SetActive(false);
            bucket.rim=Group("Bucket mouth",g);bucket.rim.localPosition=Vector3.up*.32f;
            bucket.pourStream=Particles("Poured water",bucket.rim,new Vector3(0,0,.12f),false,true);
            var main=bucket.pourStream.main;main.startColor=new Color(.65f,.85f,1,.75f);main.startSpeed=3;main.startSize=.035f;
            var collider=g.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.up*.16f;collider.size=new Vector3(.34f,.32f,.34f);
            var interaction=g.gameObject.AddComponent<TrainingInteractable>();interaction.session=session;interaction.action=InteractionAction.GrabBucket;interaction.bucket=bucket;
            allBuckets.Add(bucket);
        }
        static Mesh BucketMesh()
        {
            string folder="Assets/FireSafety/Generated/Meshes";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            string path=folder+"/Bucket.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            const int segments=24;var vertices=new List<Vector3>();var triangles=new List<int>();
            float[] radius={.12f,.17f,.155f,.105f};float[] height={0,.32f,.32f,.025f};
            for(int ring=0;ring<4;ring++)for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;vertices.Add(new Vector3(Mathf.Cos(a)*radius[ring],height[ring],Mathf.Sin(a)*radius[ring]));}
            int center=vertices.Count;vertices.Add(Vector3.zero);vertices.Add(Vector3.up*.025f);
            for(int i=0;i<segments;i++)
            {
                int a=i,b=segments+1+i,c=2*(segments+1)+i,d=3*(segments+1)+i;
                triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1, d,d+1,c,d+1,c+1,c, b,c,b+1,b+1,c,c+1, center,a,a+1,center+1,d+1,d});
            }
            mesh=new Mesh{name="Hollow bucket"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void BuildStairs(float z,RouteKind kind)
        {
            var stairs=Group(kind.ToString()+" tower",root);
            Box("Ground landing",stairs,new Vector3(4.4f,-.08f,z+.9f),new Vector3(5.8f,.16f,3.95f),tile);
            Box("Tower roof",stairs,new Vector3(4.4f,10.6f,z+.9f),new Vector3(5.8f,.15f,4),white);
            Box("Tower outside wall",stairs,new Vector3(7.25f,5.25f,z+.9f),new Vector3(.2f,10.5f,4),cream);
            for(int side=-1;side<=1;side+=2)
            {
                float sideZ=z+.9f+side*2;
                if(kind==RouteKind.PrimaryStairs)TowerWallWithRoomDoors(stairs,sideZ);
                else Box("Tower side wall",stairs,new Vector3(4.4f,5.25f,sideZ),new Vector3(5.8f,10.5f,.15f),cream);
            }
            for(int f=0;f<3;f++)
            {
                float y=f*3.5f;
                // Floor landings join corridor openings at both parallel flights.
                Box("Stair landing "+f,stairs,new Vector3(1.95f, y-.08f,z+(f==2?0:.9f)),new Vector3(.9f,.16f,f==2?1.7f:3.95f),tile);
                int entry=graph.Add(kind+" entrance "+f,new Vector3(2.05f,y,z),kind);
                graph.Link(floorNodes[f][kind==RouteKind.PrimaryStairs?4:7],entry);
                if(f==0)continue;
                float upper=y;float mid=y-1.75f;float lower=y-3.5f;
                BuildFlight(stairs,new Vector3(2.4f,upper,z),new Vector3(5.2f,mid,z));
                BuildFlight(stairs,new Vector3(5.2f,mid,z+1.8f),new Vector3(2.4f,lower,z+1.8f));
                Box("Intermediate landing",stairs,new Vector3(6.1f,mid-.08f,z+.9f),new Vector3(1.8f,.16f,3.95f),tile);
                for(int s=0;s<10;s++)
                {
                    float t=(s+.5f)/10;float bottom=Mathf.Lerp(lower,mid,t);float top=Mathf.Lerp(upper,mid,t)+1;
                    Box("Stair divider guard",stairs,new Vector3(2.4f+t*2.8f,(top+bottom)/2,z+.9f),new Vector3(.28f,top-bottom,.08f),trim);
                }
                Box("Landing window",stairs,new Vector3(7.13f,mid+1.5f,z+.9f),new Vector3(.04f,1.4f,1.3f),glass,false);
                if(kind==RouteKind.PrimaryStairs)
                {
                    BuildHalfFloorRoom(stairs,f,mid,z-.1f-1, -1);
                    BuildHalfFloorRoom(stairs,f,mid,z+.9f+2, 1);
                }
                // Rails follow each flight rather than crossing the walking line.
                Rail(stairs,new Vector3(2.4f,upper+1,z+.83f),new Vector3(5.2f,mid+1,z+.83f));
                Rail(stairs,new Vector3(5.2f,mid+1,z+.97f),new Vector3(2.4f,lower+1,z+.97f));
                if(f==2)Box("Upper shaft guard",stairs,new Vector3(1.65f,y+.55f,z+1.8f),new Vector3(.1f,1.1f,1.7f),trim);
                int a=graph.Add(kind+" upper "+f,new Vector3(2.4f,upper,z),kind);graph.Link(entry,a);
                int b=graph.Add(kind+" first bottom "+f,new Vector3(5.5f,mid,z),kind);graph.Link(a,b);
                int c=graph.Add(kind+" mid turn "+f,new Vector3(5.8f,mid,z+1.8f),kind);graph.Link(b,c);
                int d=graph.Add(kind+" return top "+f,new Vector3(5.1f,mid,z+1.8f),kind);graph.Link(c,d);
                int e=graph.Add(kind+" return bottom "+f,new Vector3(2.05f,lower,z+1.8f),kind);graph.Link(d,e);
                int exit=graph.Add(kind+" lower corridor "+f,new Vector3(0,lower,z+1.8f));graph.Link(e,exit);graph.Link(exit,floorNodes[f-1][kind==RouteKind.PrimaryStairs?4:7]);
            }
            for(int f=0;f<3;f++)routeFires.Add(Hazard(kind+" obstruction "+f,new Vector3(2.2f,f*3.5f+.06f,z),true));
        }
        static void BuildFlight(Transform parent,Vector3 start,Vector3 end)
        {
            for(int s=0;s<10;s++)
            {
                float t=(s+.5f)/10;Vector3 p=Vector3.Lerp(start,end,t);p.y-=.09f;
                Box("Stair tread",parent,p,new Vector3(.28f,.18f,1.65f),trim,false);
            }
            var ramp=Box("Continuous stair collider",parent,(start+end)*.5f+Vector3.down*.06f,new Vector3(Vector3.Distance(start,end),.12f,1.65f),tile);
            ramp.GetComponent<Renderer>().enabled=false;
            Vector3 delta=end-start;ramp.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
        static void TowerWallWithRoomDoors(Transform parent,float z)
        {
            Box("Stair enclosure",parent,new Vector3(3.45f,5.25f,z),new Vector3(3.9f,10.5f,.15f),cream);
            Box("Stair enclosure",parent,new Vector3(7.0f,5.25f,z),new Vector3(.6f,10.5f,.15f),cream);
            float bottom=0;
            foreach(float opening in new[]{1.75f,5.25f})
            {
                float height=opening-bottom;if(height>0)Box("Wall under half-floor doorway",parent,new Vector3(6.05f,bottom+height/2,z),new Vector3(1.3f,height,.15f),cream);
                bottom=opening+2.3f;
            }
            Box("Wall above half-floor doorway",parent,new Vector3(6.05f,(bottom+10.5f)/2,z),new Vector3(1.3f,10.5f-bottom,.15f),cream);
        }
        static void BuildHalfFloorRoom(Transform parent,int level,float y,float wallZ,int side)
        {
            string roomName=(level==1?"0.5":"1.5")+(side<0?"A OFFICE":"B LAB");
            var room=Group("Half-floor "+roomName,parent);
            float centerZ=wallZ+side*1.95f;
            Box("Half-floor room floor",room,new Vector3(5.9f,y-.08f,centerZ),new Vector3(2.8f,.16f,3.9f),tile);
            Box("Half-floor ceiling",room,new Vector3(5.9f,y+3.25f,centerZ),new Vector3(2.8f,.12f,3.9f),white);
            for(int s=-1;s<=1;s+=2)Box("Room side wall",room,new Vector3(5.9f+s*1.45f,y+1.6f,centerZ),new Vector3(.1f,3.2f,3.9f),cream);
            Box("Room back wall",room,new Vector3(5.9f,y+1.6f,centerZ+side*2),new Vector3(3,3.2f,.1f),cream);
            Box("Room window",room,new Vector3(5.9f,y+1.7f,centerZ+side*1.93f),new Vector3(1.6f,1.25f,.03f),glass,false);
            var doorway=Group("Half-floor door "+roomName,room);doorway.localPosition=new Vector3(5.49f,y,wallZ);
            var door=doorway.gameObject.AddComponent<TrainingDoor>();door.session=session;door.roomName=roomName;door.openAngle=side<0?95:-95;
            door.leaf=Group("Hinge",doorway);
            var leaf=Box("Door leaf",door.leaf,new Vector3(.56f,1.12f,0),new Vector3(1.12f,2.24f,.06f),wood);
            var interact=leaf.AddComponent<TrainingInteractable>();interact.action=InteractionAction.Door;interact.door=door;interact.session=session;allDoors.Add(door);
            Sign(room,new Vector3(6.05f,y+2.6f,wallZ-side*.09f),roomName,1.2f,Quaternion.Euler(0,side<0?180:0,0),navy);
            Box("Room desk",room,new Vector3(5.9f,y+.76f,centerZ+side*.55f),new Vector3(1.65f,.08f,.7f),wood);
            for(int s=-1;s<=1;s+=2)Box("Desk support",room,new Vector3(5.9f+s*.7f,y+.36f,centerZ+side*.55f),new Vector3(.08f,.72f,.6f),metal);
            Box("Monitor",room,new Vector3(5.9f,y+1.04f,centerZ+side*.6f),new Vector3(.6f,.4f,.05f),navy,false);
            Box("Chair",room,new Vector3(5.9f,y+.4f,centerZ-side*.4f),new Vector3(.5f,.7f,.5f),navy);
            Box("Half-floor light",room,new Vector3(5.9f,y+3.15f,centerZ),new Vector3(.28f,.05f,1),Mat("Light white",Color.white,true),false);
        }
        static void Rail(Transform parent,Vector3 a,Vector3 b)
        {
            var rail=Box("Handrail",parent,(a+b)/2,new Vector3(Vector3.Distance(a,b),.055f,.055f),metal,false);
            var d=b-a;rail.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
            for(int i=0;i<4;i++){var p=Vector3.Lerp(a,b,i/3f);Box("Rail post",parent,p-Vector3.up*.5f,new Vector3(.04f,1,.04f),metal,false);}
        }
        static void BuildExterior()
        {
            var outside=Group("IIT KGP ISE reference entrance",root);
            Box("Campus ground",outside,new Vector3(0,-.26f,15),new Vector3(50,.2f,65),grass);
            Box("Courtyard",outside,new Vector3(-14,-.13f,15),new Vector3(16,.24f,38),asphalt);
            // Entrance is directly opposite the primary stair opening, as described by the user.
            Box("Entrance passage floor",outside,new Vector3(-4.4f,-.08f,13),new Vector3(6,.16f,4),tile);
            Box("Entrance passage ceiling",outside,new Vector3(-4.4f,3.45f,13),new Vector3(6,.12f,4),white);
            for(int side=-1;side<=1;side+=2)Box("Entrance passage wall",outside,new Vector3(-4.4f,1.7f,13+side*2.075f),new Vector3(6,3.4f,.15f),cream);
            Box("Entry apron",outside,new Vector3(-8.5f,-.07f,13),new Vector3(3.4f,.14f,5.5f),tile);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Brown entrance surround",outside,new Vector3(-7.16f,1.65f,13+side*2.175f),new Vector3(.35f,3.3f,.35f),wood);
                Box("White facade column",outside,new Vector3(-7.03f,5.25f,13+side*3.8f),new Vector3(.28f,10.5f,.28f),white);
                LatticeScreen(outside,new Vector3(-7.0f,6.4f,13+side*2.4f),2.25f,7.8f);
                Box("Planting bed",outside,new Vector3(-12,-.015f,13+side*9),new Vector3(7,.09f,5),grass);
                for(int j=0;j<3;j++)
                {
                    float z=13+side*(5+j*3);
                    Shape("Tree trunk",PrimitiveType.Cylinder,outside,new Vector3(-12.5f,1.4f,z),new Vector3(.22f,1.4f,.22f),wood);
                    Shape("Tree crown",PrimitiveType.Sphere,outside,new Vector3(-12.5f,3.6f,z),new Vector3(2.6f,2.7f,2.6f),grass);
                }
                Shape("Terracotta planter",PrimitiveType.Cylinder,outside,new Vector3(-8.2f,.25f,13+side*3),new Vector3(.55f,.25f,.55f),Mat("Terracotta",new Color(.46f,.20f,.11f)));
                Shape("Entrance shrub",PrimitiveType.Sphere,outside,new Vector3(-8.2f,.8f,13+side*3),new Vector3(.9f,.85f,.9f),grass);
            }
            Box("Brown canopy",outside,new Vector3(-7.65f,3.2f,13),new Vector3(1.4f,.22f,4.7f),wood);
            LatticeScreen(outside,new Vector3(-7,7.25f,13),2.2f,6.2f);
            Sign(outside,new Vector3(-7.3f,3.7f,13),"DEPARTMENT OF INDUSTRIAL & SYSTEMS ENGINEERING",7.3f,Quaternion.Euler(0,90,0),Mat("Department blue",new Color(.035f,.10f,.40f)));
            Sign(outside,new Vector3(-7.3f,3.35f,13),"INDIAN INSTITUTE OF TECHNOLOGY KHARAGPUR",7.3f,Quaternion.Euler(0,90,0),Mat("Department blue",new Color(.035f,.10f,.40f)));
            Box("Assembly pad",outside,new Vector3(-16,.006f,13),new Vector3(6,.015f,8),teal,false);
            Sign(outside,new Vector3(-19.1f,2.2f,13),"ASSEMBLY POINT",3,Quaternion.Euler(0,-90,0));
            Box("Assembly sign post",outside,new Vector3(-19.15f,1,13),new Vector3(.06f,2.2f,.06f),metal);
            Equipment(outside,new Vector3(-16,1.25f,8.7f),"EMERGENCY CALL",InteractionAction.Assistance,teal);
            BuildPhotoExterior(outside);
            int exit=graph.Add("Main entrance opposite Stair A",new Vector3(-8,0,13),RouteKind.Outside);graph.Link(floorNodes[0][4],exit);
            int assembly=graph.Add("Assembly",new Vector3(-16,0,13),RouteKind.Assembly);graph.Link(exit,assembly);
            // Right of the lift when facing its doors; exact width/offset are provisional.
            var second=Group("Second ground exit right of lift",outside);
            Box("Second exit passage floor",second,new Vector3(-4.4f,-.08f,.85f),new Vector3(6,.16f,1.3f),tile);
            Box("Second exit passage ceiling",second,new Vector3(-4.4f,3.45f,.85f),new Vector3(6,.12f,1.3f),white);
            foreach(int side in new[]{-1,1})Box("Second exit passage wall",second,new Vector3(-4.4f,1.7f,.85f+side*.725f),new Vector3(6,3.4f,.15f),cream);
            Box("Second exit apron",second,new Vector3(-7.7f,-.08f,.85f),new Vector3(1.3f,.16f,1.8f),tile);
            Sign(second,new Vector3(-1.38f,2.6f,.85f),"SECOND EXIT",1.25f,Quaternion.Euler(0,-90,0));
            Sign(second,new Vector3(0,2.7f,-.15f),"GROUND EXIT  <",1.8f);
            int approach=graph.Add("Second exit lobby approach",new Vector3(0,0,.85f));graph.Link(floorNodes[0][0],approach);graph.Link(floorNodes[0][1],approach);
            int passage=graph.Add("Second exit passage",new Vector3(-4.4f,0,.85f));graph.Link(approach,passage);
            int secondOutside=graph.Add("Second ground exit beside lift",new Vector3(-8,0,.85f),RouteKind.Outside);graph.nodes[secondOutside].secondaryExit=true;
            int courtyard=graph.Add("Second exit courtyard walkway",new Vector3(-9.5f,0,8.2f));
            graph.Link(passage,secondOutside);graph.Link(secondOutside,courtyard);graph.Link(courtyard,assembly);
        }
        static void BuildPhotoExterior(Transform outside)
        {
            // Architectural cues from the four user photographs; placement is an approximation.
            var weather=Mat("Aged exterior plaster",new Color(.72f,.71f,.64f));
            var band=Mat("Weathered peach facade",new Color(.55f,.43f,.35f));
            for(int bay=0;bay<5;bay++)
            {
                float z=2+bay*6.4f;
                Box("Photo facade vertical pier",outside,new Vector3(7.12f,5.2f,z),new Vector3(.28f,10.4f,.34f),weather);
                if(bay<4)
                {
                    LatticeScreen(outside,new Vector3(7.0f,4.65f,z+3.2f),5.8f,5.1f);
                    Box("Photo facade horizontal band",outside,new Vector3(7.15f,6.9f,z+3.2f),new Vector3(.3f,.38f,6.3f),band);
                    Box("Upper ribbon window",outside,new Vector3(7.01f,9.0f,z+3.2f),new Vector3(.09f,1.15f,5.5f),glass,false);
                    for(int m=0;m<5;m++)Box("Ribbon window mullion",outside,new Vector3(7.1f,9,z+.55f+m*1.12f),new Vector3(.12f,1.2f,.055f),white,false);
                    Box("Outdoor AC unit",outside,new Vector3(7.3f,.95f,z+2),new Vector3(.48f,.55f,.8f),cream,false);
                    Shape("AC fan grille",PrimitiveType.Cylinder,outside,new Vector3(7.56f,.95f,z+2),new Vector3(.39f,.03f,.39f),metal).transform.localRotation=Quaternion.Euler(0,0,90);
                }
            }
            Box("Side walkway",outside,new Vector3(10,-.11f,14),new Vector3(5,.18f,35),asphalt);
            Box("Bicycle shelter roof",outside,new Vector3(10.8f,2.7f,7),new Vector3(4.2f,.12f,10),metal);
            for(int post=0;post<3;post++)Box("Shelter steel post",outside,new Vector3(12.6f,1.3f,3+post*4),new Vector3(.07f,2.6f,.07f),trim);
            for(int bike=0;bike<7;bike++)BuildBicycle(outside,new Vector3(10.5f,0,3+1.15f*bike));
            // Tall glazed service volume and rooftop tank visible in the supplied rear photograph.
            Box("Photo tall glazing",outside,new Vector3(-2,5.15f,34.9f),new Vector3(1.6f,9.9f,.08f),glass,false);
            for(int bar=0;bar<9;bar++)Box("Tall glazing crossbar",outside,new Vector3(-2,.7f+bar*1.1f,35),new Vector3(1.7f,.055f,.08f),trim,false);
            Shape("Rooftop water tank",PrimitiveType.Cylinder,outside,new Vector3(4.7f,11.1f,27),new Vector3(1.2f,.65f,1.2f),trim);
        }
        static void BuildBicycle(Transform parent,Vector3 p)
        {
            var bike=Group("Parked bicycle",parent);bike.localPosition=p;
            foreach(float x in new[]{-.65f,.65f})
            {
                var wheel=Group("Bicycle wheel",bike);wheel.localPosition=new Vector3(x,.4f,0);
                var line=wheel.gameObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=16;line.startWidth=line.endWidth=.045f;line.sharedMaterial=trim;
                for(int v=0;v<16;v++){float a=v*Mathf.PI*2/16;line.SetPosition(v,new Vector3(Mathf.Cos(a)*.39f,Mathf.Sin(a)*.39f,0));}
            }
            Vector3[] frame={new Vector3(-.65f,.4f,0),new Vector3(-.18f,.8f,0),new Vector3(.1f,.4f,0),new Vector3(-.65f,.4f,0),new Vector3(.4f,.85f,0),new Vector3(.65f,.4f,0),new Vector3(.1f,.4f,0),new Vector3(.4f,.85f,0),new Vector3(-.18f,.8f,0)};
            var tubes=Group("Bicycle frame",bike).gameObject.AddComponent<LineRenderer>();tubes.useWorldSpace=false;tubes.positionCount=frame.Length;tubes.SetPositions(frame);tubes.startWidth=tubes.endWidth=.035f;tubes.sharedMaterial=teal;
            Box("Bicycle saddle",bike,new Vector3(-.18f,.91f,0),new Vector3(.3f,.06f,.16f),trim,false);
            Box("Bicycle handlebars",bike,new Vector3(.43f,1,0),new Vector3(.08f,.06f,.45f),metal,false);
        }
        static void LatticeScreen(Transform parent,Vector3 center,float width,float height)
        {
            var screen=Group("Perforated facade screen",parent);
            var concrete=Mat("Screen concrete",new Color(.66f,.68f,.65f));
            for(float z=-width/2;z<=width/2;z+=.22f)Box("Screen upright",screen,center+new Vector3(0,0,z),new Vector3(.07f,height,.035f),concrete,false);
            for(float y=-height/2;y<=height/2;y+=.22f)Box("Screen crossbar",screen,center+new Vector3(0,y,0),new Vector3(.07f,.035f,width),concrete,false);
        }
        static void BuildLift(Transform[] left,Transform[] right)
        {
            var liftRoot=Group("Elevator",root);var l=liftRoot.gameObject.AddComponent<TrainingLift>();l.session=session;session.lift=l;l.leftLandingDoors=left;l.rightLandingDoors=right;
            Box("Shaft back",liftRoot,new Vector3(-4.3f,5,-2),new Vector3(.15f,10.5f,2.6f),cream);
            for(int s=-1;s<=1;s+=2)Box("Shaft wall",liftRoot,new Vector3(-3.05f,5,-2+s*1.3f),new Vector3(2.6f,10.5f,.15f),cream);
            var cabin=Group("Cabin",liftRoot);cabin.localPosition=new Vector3(-3.05f,7,-2);l.cabin=cabin;
            Box("Cabin floor",cabin,new Vector3(0,-.06f,0),new Vector3(2.35f,.12f,2.35f),metal);
            Box("Cabin back",cabin,new Vector3(-1.15f,1.15f,0),new Vector3(.1f,2.3f,2.35f),metal);
            for(int s=-1;s<=1;s+=2)Box("Cabin side",cabin,new Vector3(0,1.15f,s*1.15f),new Vector3(2.35f,2.3f,.1f),metal);
            Box("Cabin roof",cabin,new Vector3(0,2.3f,0),new Vector3(2.35f,.1f,2.35f),white);
            l.leftCabinDoor=Box("Cabin left",cabin,new Vector3(1.16f,1.1f,-.39f),new Vector3(.07f,2.2f,.78f),metal).transform;
            l.rightCabinDoor=Box("Cabin right",cabin,new Vector3(1.16f,1.1f,.39f),new Vector3(.07f,2.2f,.78f),metal).transform;
            for(int f=0;f<3;f++)
            {
                var b=Box("Lift floor "+f,cabin,new Vector3(.7f,1+f*.2f,.99f),new Vector3(.14f,.14f,.06f),white);
                var i=b.AddComponent<TrainingInteractable>();i.session=session;i.action=InteractionAction.LiftFloor;i.value=f;
                Text("Button number",b.transform,new Vector3(0,0,-.6f),f.ToString(),.15f,Color.black,Quaternion.identity);
            }
        }
        static ParticleSystem Particles(string name,Transform parent,Vector3 p,bool smoke,bool discharge=false)
        {
            var g=Group(name,parent);g.localPosition=p;var ps=g.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.maxParticles=smoke?45:discharge?70:65;
            main.startLifetime=smoke?3.5f:discharge?.6f:.85f;main.startSpeed=smoke?.5f:discharge?5:1.2f;
            main.startSize=smoke?.8f:discharge?.12f:.35f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startColor=smoke?new Color(.16f,.17f,.18f,.32f):discharge?new Color(.85f,.91f,.93f,.7f):new Color(1,.34f,.025f,.95f);
            var emission=ps.emission;emission.rateOverTime=smoke?9:discharge?45:42;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=smoke?18:discharge?5:12;shape.radius=smoke?.4f:discharge?.025f:.22f;
            if(!discharge)g.localRotation=Quaternion.Euler(-90,0,0);
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,smoke?.65f:1,1,smoke?1.3f:0));
            var col=ps.colorOverLifetime;col.enabled=true;var grad=new Gradient();grad.SetKeys(new[]{new GradientColorKey(smoke?new Color(.2f,.21f,.22f):discharge?Color.white:Color.yellow,0),new GradientColorKey(smoke?new Color(.3f,.31f,.32f):discharge?Color.white:new Color(.8f,.1f,0),1)},new[]{new GradientAlphaKey(smoke?.25f:.8f,0),new GradientAlphaKey(0,1)});col.color=grad;
            if(smoke)
            {
                // Vertex color and lifetime color multiply: the old .32 * .25 alpha made smoke almost invisible.
                grad.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.82f,.84f,.86f),1)},
                    new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(.95f,.15f),new GradientAlphaKey(.75f,.75f),new GradientAlphaKey(0,1)});col.color=grad;
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.x=.05f;velocity.y=.06f;velocity.z=.025f;
                var noise=ps.noise;noise.enabled=true;noise.strength=.12f;noise.frequency=.35f;noise.scrollSpeed=.2f;noise.quality=ParticleSystemNoiseQuality.Low;
            }
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=ParticleMaterial(smoke);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return ps;
        }
        static Material ParticleMaterial(bool smoke=false)
        {
            string path="Assets/FireSafety/Generated/Materials/"+(smoke?"Smoke":"Particles")+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(m,path);}
            m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;
            // Soft radial texture avoids opaque square billboards.
            string texturePath="Assets/FireSafety/Generated/"+(smoke?"SmokeCloud":"Particle")+".png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(texture==null)
            {
                int pixels=smoke?128:32;float center=(pixels-1)*.5f;texture=new Texture2D(pixels,pixels,TextureFormat.RGBA32,false);
                for(int y=0;y<pixels;y++)for(int x=0;x<pixels;x++)
                {
                    float d=Vector2.Distance(new Vector2(x,y),new Vector2(center,center))/center;
                    float radial=Mathf.Clamp01(1-d),alpha=smoke?Mathf.Pow(radial,.8f)*(.55f+.45f*Mathf.PerlinNoise(x*.055f+11,y*.055f+7)):radial*radial;
                    texture.SetPixel(x,y,new Color(1,1,1,alpha));
                }
                texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(texturePath);texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }
            m.SetTexture("_BaseMap",texture);m.SetColor("_BaseColor",Color.white);return m;
        }
        static TrainingHazard Hazard(string name,Vector3 p,bool route)
        {
            var g=Group(name,root);g.position=p;var h=g.gameObject.AddComponent<TrainingHazard>();h.session=session;h.isRouteFire=route;
            h.flames=Particles("Flames",g,Vector3.zero,false);h.smoke=Particles("Smoke",g,Vector3.up*.7f,true);
            var light=Group("Fire glow",g).gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=4;light.color=new Color(1,.3f,.06f);light.shadows=LightShadows.None;h.glow=light;
            h.fireAudio=Audio(g,"Fire crackle",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FireSafety/Audio/FireCrackle.wav"),true,true,.4f);return h;
        }
        static AudioClip Clip(string name)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FireSafety/Audio/"+name+".wav");
            if(clip==null)throw new InvalidOperationException("Missing training audio: "+name+". Run Tools/Generate-TrainingAudio.ps1.");
            return clip;
        }
        static AudioSource Audio(Transform parent,string name,AudioClip clip,bool spatial,bool loop,float volume)
        {
            var source=Group(name,parent).gameObject.AddComponent<AudioSource>();source.clip=clip;source.playOnAwake=false;source.loop=loop;source.volume=volume;
            source.spatialBlend=spatial?1:0;source.rolloffMode=AudioRolloffMode.Linear;source.minDistance=2;source.maxDistance=16;source.dopplerLevel=0;return source;
        }
        static void BuildAudio()
        {
            var audio=root.gameObject.AddComponent<TrainingAudio>();session.audioDirector=audio;audio.session=session;
            audio.narration=Audio(root,"Briefing and equipment voice",null,false,false,.85f);
            audio.fireBriefings=new[]{Clip("BriefOrdinary"),Clip("BriefElectrical"),Clip("BriefLiquid")};
            audio.desktopControls=Clip("ControlsDesktop");audio.vrControls=Clip("ControlsVR");audio.equipment=Clip("Equipment");audio.soundCheck=Clip("SoundCheck");
            audio.alarmAcknowledged=Clip("AlarmAcknowledged");audio.callAcknowledged=Clip("CallAcknowledged");
            audio.fireVoices=new[]{Clip("NPCFireA"),Clip("NPCFireB")};audio.alarmVoices=new[]{Clip("NPCAlarmA"),Clip("NPCAlarmB")};audio.waitingVoices=new[]{Clip("NPCWaitingA"),Clip("NPCWaitingB")};
            audio.theoryLessons=new AudioClip[TrainingTheory.Course.pages.Length];
            for(int i=0;i<audio.theoryLessons.Length;i++)audio.theoryLessons[i]=Clip("Theory"+(i+1).ToString("00"));
            audio.theoryQuestions=new AudioClip[TrainingTheory.Course.questions.Length];audio.theoryAnswers=new AudioClip[audio.theoryQuestions.Length];
            for(int i=0;i<audio.theoryQuestions.Length;i++){audio.theoryQuestions[i]=Clip("TheoryQuestion"+(i+1).ToString("00"));audio.theoryAnswers[i]=Clip("TheoryAnswer"+(i+1).ToString("00"));}
        }
        static void BuildPlayer()
        {
            var g=Group("XR and Desktop Player",root);g.SetPositionAndRotation(session.spawnPoints[2].position,session.spawnPoints[2].rotation);g.gameObject.tag="Player";g.gameObject.layer=9;
            var cc=g.gameObject.AddComponent<CharacterController>();cc.height=1.7f;cc.radius=.22f;cc.center=Vector3.up*.85f;cc.stepOffset=.28f;cc.slopeLimit=45;cc.skinWidth=.025f;
            var p=g.gameObject.AddComponent<TrainingXRPlayer>();session.player=p;p.session=session;
            var tracking=Group("Tracking space",g);p.trackingSpace=tracking;
            var head=Group("Head camera",tracking);head.localPosition=Vector3.up*1.65f;p.Head=head;
            var camera=head.gameObject.AddComponent<Camera>();camera.nearClipPlane=.06f;camera.farClipPlane=95;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.48f,.65f,.73f);camera.fieldOfView=75;camera.cullingMask=~(1<<5);head.gameObject.tag="MainCamera";head.gameObject.AddComponent<AudioListener>();
            var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=false;cameraData.renderShadows=true;
            var overlay=Group("Stereo interface camera",head).gameObject.AddComponent<Camera>();overlay.CopyFrom(camera);overlay.cullingMask=1<<5;overlay.clearFlags=CameraClearFlags.Depth;
            var overlayData=overlay.GetUniversalAdditionalCameraData();overlayData.renderType=CameraRenderType.Overlay;overlayData.renderShadows=false;cameraData.cameraStack.Add(overlay);
            var notice=Group("Tracking recovery notice",head);notice.localPosition=new Vector3(0,-.25f,1);
            Box("Notice background",notice,Vector3.zero,new Vector3(1.65f,.3f,.025f),navy,false);
            p.trackingNoticeText=Text("Notice text",notice,new Vector3(0,0,-.02f),"VR CONNECTION",.012f,Color.white,Quaternion.identity);
            foreach(var child in notice.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            p.trackingNotice=notice.gameObject;notice.gameObject.SetActive(false);
            p.leftHand=Group("Left controller",tracking);p.rightHand=Group("Right controller",tracking);
            Shape("Left controller body",PrimitiveType.Capsule,p.leftHand,Vector3.zero,new Vector3(.055f,.06f,.055f),navy);
            Shape("Right controller body",PrimitiveType.Capsule,p.rightHand,Vector3.zero,new Vector3(.055f,.06f,.055f),navy);
            var line=Group("Interaction ray",g).gameObject.AddComponent<LineRenderer>();line.positionCount=2;line.startWidth=.004f;line.endWidth=.002f;line.useWorldSpace=true;line.sharedMaterial=Mat("Pointer",new Color(.3f,.8f,1),true);line.shadowCastingMode=ShadowCastingMode.Off;p.pointer=line;
            TrainingControllerSetup.Configure(p);
        }
        static void BuildPresentation()
        {
            var g=Group("Assessment interface",root);var ui=g.gameObject.AddComponent<TrainingPresentation>();ui.session=session;session.presentation=ui;
            var panel=Group("World-space assessment panel",g);panel.position=session.player.Head.position+Vector3.forward*2;ui.panel=panel;
            Box("Panel",panel,Vector3.zero,new Vector3(2.9f,2.35f,.06f),navy);
            Box("Top accent",panel,new Vector3(0,1.15f,-.04f),new Vector3(2.9f,.035f,.02f),teal,false);
            ui.heading=Text("Heading",panel,new Vector3(-1.3f,1.01f,-.05f),"ASSESSMENT",.025f,Color.white,Quaternion.identity,TextAnchor.UpperLeft);
            ui.body=Text("Body",panel,new Vector3(-1.3f,.77f,-.05f),"",.014f,new Color(.85f,.91f,.92f),Quaternion.identity,TextAnchor.UpperLeft);ui.body.lineSpacing=.9f;
            ui.footer=Text("Footer",panel,new Vector3(-1.3f,-1.09f,-.05f),"",.014f,new Color(.55f,.76f,.76f),Quaternion.identity,TextAnchor.UpperLeft);
            ui.buttons=new TrainingInteractable[3];ui.buttonLabels=new TextMesh[3];
            for(int i=0;i<3;i++)
            {
                var b=Box("Interface button "+i,panel,new Vector3(-.96f+i*.96f,-.87f,-.06f),new Vector3(.88f,.27f,.06f),teal);
                var t=Text("Button label",b.transform,new Vector3(0,0,-.6f),"",.011f,Color.white,Quaternion.identity);t.transform.localScale=new Vector3(1/.88f,1/.27f,1/.06f);
                var a=b.AddComponent<TrainingInteractable>();a.session=session;ui.buttons[i]=a;ui.buttonLabels[i]=t;
            }
            foreach(var child in panel.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
        }
        static void BuildOccupants()
        {
            Color[] clothes={new Color(.15f,.32f,.46f),new Color(.52f,.25f,.16f),new Color(.25f,.39f,.31f),new Color(.50f,.39f,.57f)};
            for(int i=0;i<12;i++)
            {
                int f=i/4,local=i%4;bool corridor=f==0&&local==3;
                var g=Group((local<2?"Student ":local==2?"Teacher ":"Staff ")+(i+1).ToString("00"),root);
                g.position=new Vector3(local==0?4f:local==1?5.7f:local==2?4.8f:corridor?.4f:-3.85f,f*3.5f+.04f,local<2?3.1f:local==2?5.15f:corridor?18:9.15f);
                g.rotation=Quaternion.Euler(0,local<2?0:local==2?180:270,0);g.gameObject.layer=8;
                var cc=g.gameObject.AddComponent<CharacterController>();cc.height=1.7f;cc.radius=.2f;cc.center=Vector3.up*.85f;cc.stepOffset=.28f;cc.slopeLimit=45;cc.skinWidth=.025f;
                var n=g.gameObject.AddComponent<TrainingNPC>();n.session=session;n.graph=graph;n.identity=i;n.role=local<2?OccupantRole.Student:local==2?OccupantRole.Teacher:OccupantRole.Staff;
                n.seated=local<2||local==3&&!corridor;n.corridorDuty=corridor;
                n.workplaceDoor=corridor?null:local<3?session.incidentDoors[f]:allDoors.Find(d=>d.roomName=="STAFF OFFICE "+f+"02");
                n.responseDelay=local==2?1.2f:local==3?1.8f:2.7f+local*.9f;n.speed=1.05f+(i%4)*.1f;
                n.voice=Audio(g,"Occupant voice",null,true,false,.9f);n.voice.transform.localPosition=Vector3.up*1.6f;
                var shirt=Mat("Shirt "+i%4,clothes[i%4]);var pants=Mat("Trousers "+i%3,new Color(.10f+i%3*.04f,.13f+i%3*.03f,.17f));
                Shape("Torso",PrimitiveType.Capsule,g,new Vector3(0,1.15f,0),new Vector3(.39f,.3f,.25f),shirt);
                Shape("Head",PrimitiveType.Sphere,g,new Vector3(0,1.63f,0),new Vector3(.23f,.28f,.23f),Mat("Skin "+i%3,new Color(.42f+i%3*.15f,.25f+i%3*.13f,.16f+i%3*.11f)));
                Shape("Neck",PrimitiveType.Cylinder,g,new Vector3(0,1.49f,0),new Vector3(.085f,.06f,.085f),skin);
                Shape("Hair",PrimitiveType.Sphere,g,new Vector3(0,1.74f,-.025f),new Vector3(.24f,.13f,.23f),trim);
                for(int eye=-1;eye<=1;eye+=2){Shape("Eye",PrimitiveType.Sphere,g,new Vector3(eye*.045f,1.66f,.107f),new Vector3(.031f,.023f,.015f),white);Shape("Pupil",PrimitiveType.Sphere,g,new Vector3(eye*.045f,1.66f,.116f),Vector3.one*.012f,trim);}
                for(int side=-1;side<=1;side+=2)
                {
                    var arm=Group(side<0?"Left arm":"Right arm",g);arm.localPosition=new Vector3(side*.245f,1.37f,0);
                    Shape("Sleeve",PrimitiveType.Capsule,arm,new Vector3(0,-.22f,0),new Vector3(.12f,.23f,.12f),shirt);
                    Shape("Hand",PrimitiveType.Sphere,arm,new Vector3(0,-.48f,0),new Vector3(.10f,.12f,.09f),skin);
                    var leg=Group(side<0?"Left leg":"Right leg",g);leg.localPosition=new Vector3(side*.11f,.9f,0);
                    Shape("Trouser leg",PrimitiveType.Capsule,leg,new Vector3(0,-.34f,0),new Vector3(.15f,.32f,.15f),pants);
                    Shape("Ankle",PrimitiveType.Cylinder,leg,new Vector3(0,-.73f,0),new Vector3(.08f,.07f,.08f),pants);
                    Box("Shoe",leg,new Vector3(0,-.81f,.045f),new Vector3(.17f,.12f,.28f),trim,false);
                    if(side<0){n.leftArm=arm;n.leftLeg=leg;}else{n.rightArm=arm;n.rightLeg=leg;}
                }
                npcs.Add(n);n.ResetOccupant();
            }
        }
        static void ConfigureLayers()
        {
            var manager=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=manager.FindProperty("layers");
            // Preserve existing named user layers; use per-collider ignores as a fallback.
            if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(8).stringValue))layers.GetArrayElementAtIndex(8).stringValue="TrainingNPC";
            if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(9).stringValue))layers.GetArrayElementAtIndex(9).stringValue="TrainingPlayer";
            manager.ApplyModifiedPropertiesWithoutUndo();
        }
        static void ConfigureRendering()
        {
            session.desktopPipeline=Pipeline("Desktop","Assets/Settings/URP-Balanced.asset");
            session.vrPipeline=Pipeline("VR","Assets/Settings/URP-Performant.asset");
        }
        static UniversalRenderPipelineAsset Pipeline(string name,string source)
        {
            string path="Assets/FireSafety/Generated/"+name+"Pipeline.asset";
            var asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if(asset==null){asset=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(source));asset.name=name+" Training Pipeline";AssetDatabase.CreateAsset(asset,path);}
            asset.msaaSampleCount=4;asset.renderScale=1;asset.supportsHDR=false;asset.supportsDynamicBatching=true;asset.shadowDistance=24;
            EditorUtility.SetDirty(asset);return asset;
        }
        static void MarkStaticGeometry()
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var g=renderer.gameObject;
                if(g.GetComponentInParent<TrainingNPC>()!=null || g.GetComponentInParent<TrainingXRPlayer>()!=null ||
                    g.GetComponentInParent<TrainingDoor>()!=null || g.GetComponentInParent<TrainingExtinguisher>()!=null ||
                    g.GetComponentInParent<TrainingBucket>()!=null ||
                    g.GetComponentInParent<TrainingLift>()!=null || g.GetComponentInParent<TrainingHazard>()!=null ||
                    g.GetComponentInParent<TrainingPresentation>()!=null || g.GetComponent<TextMesh>()!=null)continue;
                GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
            }
        }
        static void BuildFireSources(TrainingHazard fire)
        {
            fire.sourceProps=new GameObject[3];
            var ordinary=Group("Ordinary combustibles",fire.transform);fire.sourceProps[0]=ordinary.gameObject;
            Shape("Wastepaper bin",PrimitiveType.Cylinder,ordinary,new Vector3(-.12f,.24f,0),new Vector3(.28f,.24f,.35f),metal);
            for(int i=0;i<4;i++)Box("Paper",ordinary,new Vector3(-.12f,.45f+i*.01f,0),new Vector3(.18f,.02f,.2f),white,false);
            var electrical=Group("Energized electrical",fire.transform);fire.sourceProps[1]=electrical.gameObject;
            Box("Electrical enclosure",electrical,new Vector3(-.2f,.5f,0),new Vector3(.28f,1,.5f),metal);
            for(int i=0;i<3;i++)Box("Circuit breaker",electrical,new Vector3(-.04f,.35f+i*.15f,0),new Vector3(.04f,.08f,.25f),trim,false);
            Text("Electrical label",electrical,new Vector3(.01f,.83f,0),"ELECTRICAL",.016f,Color.yellow,Quaternion.Euler(0,-90,0));
            var liquid=Group("Liquid spill",fire.transform);fire.sourceProps[2]=liquid.gameObject;
            Shape("Spill",PrimitiveType.Cylinder,liquid,Vector3.up*.015f,new Vector3(.65f,.012f,.8f),Mat("Liquid amber",new Color(.35f,.19f,.025f)));
            var can=Shape("Liquid container",PrimitiveType.Cylinder,liquid,new Vector3(-.16f,.12f,.3f),new Vector3(.16f,.2f,.16f),red);can.transform.localRotation=Quaternion.Euler(0,0,80);
        }
        [MenuItem("Fire Safety/Build Windows Assessment")]
        public static void BuildWindows()
        {BuildWindowsTo("Builds/Windows");}
        public static void BuildWindowsFinal()
        {BuildWindowsTo("Builds/WindowsFinal");}
        public static void BuildWindowsReportFix()
        {BuildWindowsTo("Builds/WindowsReportFix");}
        public static void BuildWindowsSimple()
        {BuildWindowsTo("Builds/WindowsSimple");}
        public static void BuildWindowsGame()
        {BuildWindowsTo("Builds/WindowsGame");}
        public static void BuildWindowsSmoke()
        {BuildWindowsTo("Builds/WindowsSmoke");}
        static void BuildWindowsTo(string folder)
        {
            Build();Directory.CreateDirectory(folder);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName=folder+"/DepartmentFireSafety.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Windows build failed: "+result.summary.result);
            Debug.Log("FIRE_SAFETY_WINDOWS_BUILD_OK");
        }
        [MenuItem("Fire Safety/Build Quest APK")]
        public static void BuildQuest()
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
                throw new InvalidOperationException("Install Android Build Support, Android SDK/NDK and OpenJDK for Unity 2022.3.21f1 in Unity Hub first.");
            Build();Directory.CreateDirectory("Builds/Quest");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"org.departmenttraining.firesafety");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{ScenePath},locationPathName="Builds/Quest/DepartmentFireSafety.apk",target=BuildTarget.Android,options=BuildOptions.Development});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Quest build failed: "+result.summary.result);
            Debug.Log("FIRE_SAFETY_QUEST_BUILD_OK");
        }
    }
}
