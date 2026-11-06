using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FireSafety.Editor
{
    public static class TrainingVisualReview
    {
        public static void CaptureAndBuildWindows(){Capture();TrainingSceneBuilder.BuildWindows();}
        public static void BuildFinalAndCapture(){TrainingSceneBuilder.BuildWindowsFinal();Capture();}
        public static void BuildReportFixAndCapture(){TrainingSceneBuilder.BuildWindowsReportFix();Capture();}
        public static void BuildSimpleAndCapture(){TrainingSceneBuilder.BuildWindowsSimple();Capture();}
        public static void BuildGameAndCapture(){TrainingSceneBuilder.BuildWindowsGame();Capture();}
        public static void BuildSmokeAndCapture(){TrainingSceneBuilder.BuildWindowsSmoke();Capture();}
        [MenuItem("Fire Safety/Capture Review Images")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(TrainingSceneBuilder.ScenePath);
            var session=Object.FindObjectOfType<TrainingSession>();
            var cam=session.player.Head.GetComponent<Camera>();
            var previousPipeline=QualitySettings.renderPipeline;QualitySettings.renderPipeline=session.desktopPipeline;
            session.presentation.Hide();
            Directory.CreateDirectory("tmp/analysis/review");
            CaptureAt(cam,new Vector3(0,8.65f,4.8f),new Vector3(0,8.5f,22),"corridor");
            CaptureAt(cam,new Vector3(1.8f,8.6f,13),new Vector3(6,5.7f,14),"stairs");
            CaptureAt(cam,new Vector3(-2.3f,5.1f,4),new Vector3(-5.5f,4.6f,4),"office");
            CaptureAt(cam,new Vector3(-20,6.5f,5),new Vector3(-6,4,13),"exterior");
            CaptureAt(cam,new Vector3(18,5,-1),new Vector3(7,4.8f,15),"photo-exterior");
            CaptureAt(cam,new Vector3(2.1f,1.65f,4.3f),new Vector3(4.7f,1.2f,4.1f),"classroom-roles");
            CaptureAt(cam,new Vector3(.3f,1.65f,.85f),new Vector3(-8,1.3f,.85f),"second-exit");
            CaptureAt(cam,new Vector3(2.2f,8.65f,27),new Vector3(5.3f,7.8f,27),"washroom");
            CaptureAt(cam,new Vector3(0,8.65f,-3.2f),new Vector3(1.2f,8.1f,-2),"water-cooler");
            cam.transform.SetPositionAndRotation(new Vector3(.15f,8.7f,4.6f),Quaternion.identity);
            session.presentation.ShowMenu();
            CaptureCurrent(cam,"menu",true);
            for(int i=0;i<TrainingTheory.Course.pages.Length;i++){session.presentation.ShowTheory(i);CaptureCurrent(cam,"theory-"+(i+1).ToString("00"),true);}
            session.presentation.BeginTheoryQuiz();CaptureCurrent(cam,"theory-question",true);
            session.presentation.AnswerTheory(0);CaptureCurrent(cam,"theory-feedback",true);
            session.presentation.ShowAssessmentSelection();CaptureCurrent(cam,"assessments",true);
            session.presentation.ShowGameSetup(0);CaptureCurrent(cam,"game-settings",true);
            session.presentation.ShowGameMore();CaptureCurrent(cam,"building-directions",true);
            session.PrepareAssessment(1);CaptureCurrent(cam,"briefing",true);
            session.presentation.ShowControls(false);CaptureCurrent(cam,"desktop-controls",true);
            session.presentation.ShowControls(true);CaptureCurrent(cam,"vr-controls",true);
            session.presentation.ShowEquipmentGuide();CaptureCurrent(cam,"equipment-guide",true);
            session.ReturnToMenu();
            session.report=new AssessmentReport { participantCode="Review fixture",scenarioId="A01",scenario="Office fire / main stair evacuation",outcome="Reached the assembly point",durationSeconds=73,alarmSeconds=5,assistanceSeconds=12,firstResponseSeconds=2,distanceMeters=64,npcCount=12,npcsEvacuated=12,npcsMainExit=9,npcsSecondExit=3,meanFramesPerSecond=90 };
            session.report.Evaluate(true,false,true,true);cam.transform.localRotation=Quaternion.Euler(75,0,0);session.presentation.ShowReport(0);
            CaptureCurrent(cam,"report",true);
            CaptureCurrent(cam,"report-after-looking-down",true);
            session.history.Add(session.report);session.ReturnToMenu();session.presentation.ShowGameMore();CaptureCurrent(cam,"menu-last-report",true);session.ShowLatestReport();
            session.report.unsafeDecisions=3;session.report.smokeExposureSeconds=20;session.report.extinguisherAttempted=true;
            session.report.Evaluate(false,false,false,false);session.presentation.ShowReport(2);CaptureCurrent(cam,"equipment-analysis",true);session.presentation.ShowReport(3);CaptureCurrent(cam,"recommendations",true);
            for(int i=0;i<24;i++)session.report.events.Add(new AssessmentEvent {seconds=i*3,action="Route reassessed",detail="Main stair unavailable; participant retreated, closed the office door and requested assistance.",severity="observation"});
            session.presentation.ShowReport(int.MaxValue);CaptureCurrent(cam,"timeline",true);
            session.persistLatestReport=false;session.phase=SessionPhase.Ready;session.StartAssessment(6);session.fireDevelopment.Tick(50,50);
            SimulateFire(session.incident);foreach(var fire in session.fireDevelopment.zones)SimulateFire(fire);
            CaptureAt(cam,new Vector3(0,8.65f,8),new Vector3(-.4f,8.2f,16),"spreading-corridor");
            CaptureAt(cam,new Vector3(-2.3f,8.6f,15.9f),new Vector3(-3.5f,8,17.7f),"spreading-other-room");
            session.Finish("Visual fixture");session.StartAssessment(3);SimulateFire(session.incident);foreach(var fire in session.routeFires)SimulateFire(fire);
            CaptureAt(cam,new Vector3(0,8.65f,12.8f),new Vector3(2.4f,8.1f,13),"dangerous-stairs");session.Finish("Visual fixture");
            session.phase=SessionPhase.Ready;session.StartAssessment(0);SimulateFire(session.incident);
            CaptureAt(cam,new Vector3(2.35f,8.65f,4.3f),new Vector3(5.6f,8.5f,5.15f),"smoke-small");session.Finish("Visual fixture");
            session.StartAssessment(3);SimulateFire(session.incident);foreach(var fire in session.routeFires)SimulateFire(fire);
            CaptureAt(cam,new Vector3(2.35f,8.65f,4.3f),new Vector3(5.6f,8.5f,5.15f),"smoke-large");
            CaptureAt(cam,new Vector3(0,8.65f,11),new Vector3(2.4f,8.8f,13),"smoke-stairs");session.Finish("Visual fixture");
            QualitySettings.renderPipeline=previousPipeline;
            Debug.Log("FIRE_SAFETY_REVIEW_IMAGES_OK: tmp/analysis/review");
        }
        static void CaptureAt(Camera cam,Vector3 position,Vector3 target,string name)
        {cam.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));CaptureCurrent(cam,name,false);}
        static void SimulateFire(TrainingHazard fire)
        {if(!fire.Active)return;if(fire.flames!=null)fire.flames.Simulate(.6f,true,false,true);if(fire.smoke!=null){fire.smoke.Simulate(4,true,false,true);fire.ConfineSmokeToFloor();}}
        static void CaptureCurrent(Camera cam,string name,bool stack)
        {
            var rt=RenderTexture.GetTemporary(1440,1000,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            if(stack)RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest { destination=rt });
            else RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest {destination=rt});
            RenderTexture.active=rt;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();File.WriteAllBytes("tmp/analysis/review/"+name+".png",texture.EncodeToPNG());
            Object.DestroyImmediate(texture);RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
        }
    }
}
