using UnityEngine;

namespace FireSafety
{
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh))]
    public class TrainingWorldText : MonoBehaviour
    {
        TextMesh textMesh;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock properties;
        void OnEnable()
        {
            textMesh=GetComponent<TextMesh>();meshRenderer=GetComponent<MeshRenderer>();
            properties=new MaterialPropertyBlock();Font.textureRebuilt+=Rebuilt;Refresh();
        }
        void OnDisable(){Font.textureRebuilt-=Rebuilt;}
        void Rebuilt(Font font){if(textMesh!=null && font==textMesh.font)Refresh();}
        void Refresh()
        {
            if(textMesh==null || textMesh.font==null || meshRenderer==null)return;
            properties.SetTexture("_MainTex",textMesh.font.material.mainTexture);
            meshRenderer.SetPropertyBlock(properties);
        }
    }
}
