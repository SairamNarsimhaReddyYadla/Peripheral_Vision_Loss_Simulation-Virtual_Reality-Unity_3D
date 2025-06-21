using UnityEngine;

[ExecuteAlways] //run in both editor and play mode.
public class GlaucomaMaskController : MonoBehaviour  
{
    [System.Serializable]

    public class ObjectMaterialPair
    {
        public Transform objectTransform;
        public Material objectMaterial;

    }

    [Header("Assign Object and Material")]

    //using two instances of the same shader for two different scotomas.
    public ObjectMaterialPair[] scotoma1Objects;
    public ObjectMaterialPair[] scotoma2Objects;


    [Header("Shader Properties")]
    [Range(0f, 1f)] public float sphereSmoothness = 0.5f;
    public float adjustCameraDistance = 8f;


    private void Update()
    {
        UpdateMaterials(scotoma1Objects);
        UpdateMaterials(scotoma2Objects);
    }

    void UpdateMaterials(ObjectMaterialPair[] objects)
    {
        foreach(var obj in objects)
        {
            if (obj.objectTransform != null && obj.objectMaterial != null)
            {
                float radius = obj.objectTransform.localScale.x * adjustCameraDistance;
                Vector3 spherePosition = obj.objectTransform.position;


                obj.objectMaterial.SetVector("_SpherePosition", spherePosition);
                obj.objectMaterial.SetFloat("_SphereRadius", radius);
                obj.objectMaterial.SetFloat("_SphereSmoothness", sphereSmoothness);
            }
        }
    }


}


