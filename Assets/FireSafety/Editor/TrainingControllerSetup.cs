using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.CoreUtils;
using System.IO;
using UnityEngine.Rendering.Universal;

namespace FireSafety.Editor
{
    public static class TrainingControllerSetup
    {
        const string Models="Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/Prefabs/Controllers/";
        [MenuItem("Fire Safety/Configure Quest Controller Play")]
        public static void ConfigureCurrentScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before configuring controllers.");
            var player=UnityEngine.Object.FindObjectOfType<TrainingXRPlayer>();
            if(player==null)throw new InvalidOperationException("Open FireSafetyTraining first.");
            Configure(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("FIRE_SAFETY_CONTROLLERS_READY: one gameplay rig; both controller models; near and ray interaction; tracking recovery.");
        }
        // Batch entry preserves the saved department and any instructor configuration.
        public static void ConfigureSavedScene()
        {EditorSceneManager.OpenScene(TrainingSceneBuilder.ScenePath);ConfigureCurrentScene();}
        public static void BuildConfiguredWindows()
        {
            ConfigureSavedScene();
            var player=UnityEngine.Object.FindObjectOfType<TrainingXRPlayer>();
            if(player.leftControllerModel==null||player.rightControllerModel==null)throw new InvalidOperationException("Both controller models are required.");
            foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)
                    if(material==null||material.shader==null||ShaderUtil.ShaderHasError(material.shader))throw new InvalidOperationException("Controller or interface material failed validation: "+renderer.name);
            CaptureControllers(player);
            // Capture is an in-memory review fixture; ship the saved scene unchanged.
            EditorSceneManager.OpenScene(TrainingSceneBuilder.ScenePath);
            const string folder="Builds/WindowsXR";Directory.CreateDirectory(folder);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{TrainingSceneBuilder.ScenePath},locationPathName=folder+"/DepartmentFireSafety.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Quest PC Link build failed: "+result.summary.result);
            File.WriteAllText(folder+"/READ_ME.txt","Meta Quest 3 / PC Link controller build\r\nOpen Meta Horizon Link, connect Quest 3 and enter PC Link, then run DepartmentFireSafety.exe.\r\nKeep this whole WindowsXR folder together. This is a Windows PC VR app, not a standalone APK.\r\nUse CONTROLS and SOUND CHECK before PLAY.\r\nLeft stick walk; right stick snap turn; right trigger select/spray/pour; right grip tap grab/return; left trigger pin; left Y session panel.\r\nReal headset comfort, performance, floor height and physical audio still require a headset trial.\r\n");
            Debug.Log("FIRE_SAFETY_QUEST_PC_BUILD_OK: "+folder+"/DepartmentFireSafety.exe");
        }
        static void CaptureControllers(TrainingXRPlayer player)
        {
            player.session.presentation.Hide();
            QualitySettings.renderPipeline=player.session.desktopPipeline;
            player.leftHand.position=player.Head.TransformPoint(new Vector3(-.23f,-.3f,.5f));player.leftHand.rotation=player.Head.rotation;
            player.rightHand.position=player.Head.TransformPoint(new Vector3(.23f,-.3f,.5f));player.rightHand.rotation=player.Head.rotation;
            var camera=player.Head.GetComponent<Camera>();var target=new RenderTexture(1280,720,24);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
            Directory.CreateDirectory("tmp/analysis/review");File.WriteAllBytes("tmp/analysis/review/quest-controllers.png",texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("FIRE_SAFETY_CONTROLLER_REVIEW_OK");
        }
        public static void Configure(TrainingXRPlayer player)
        {
            Undo.RecordObject(player,"Configure Quest controllers");
            player.leftControllerModel=Model(player.leftHand,true,player.leftControllerModel);
            player.rightControllerModel=Model(player.rightHand,false,player.rightControllerModel);
            foreach(var rig in UnityEngine.Object.FindObjectsOfType<XROrigin>())
                if(rig.transform!=player.transform&&!player.transform.IsChildOf(rig.transform))
                {Undo.RecordObject(rig.gameObject,"Disable duplicate XR rig");rig.gameObject.SetActive(false);}
            player.walkSpeed=2;player.snapTurnAngle=30;player.interactionReach=3.5f;player.nearGrabRadius=.2f;
            EditorUtility.SetDirty(player);
        }
        static GameObject Model(Transform hand,bool left,GameObject existing)
        {
            if(existing!=null)return existing;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Models+"XR Controller "+(left?"Left":"Right")+".prefab");
            if(prefab==null)throw new InvalidOperationException("The imported XRI Starter Assets controller model is missing.");
            foreach(Transform child in hand)if(child.name.EndsWith("controller body",StringComparison.OrdinalIgnoreCase))child.gameObject.SetActive(false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,hand);
            Undo.RegisterCreatedObjectUndo(model,"Add controller model");
            // Retain the model's supplied pivot, scale and handedness corrections.
            foreach(var c in model.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true))if(behaviour!=null)behaviour.enabled=false;
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=9;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
            return model;
        }
    }
}
