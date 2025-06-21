using UnityEngine;

public class GlaucomaCameraCull : MonoBehaviour
{
    public enum MainEye { Left, Right, Both }
    public ScotomaSimulator.Eye TestEye;
    public ScotomaSimulator.DominantEyeBoth glaucomaDominantEye; //dominant eye selection in case of both eyes.
    public Camera leftCamera;
    public Camera rightCamera;
     

    private string normalLayer = "NormalView";  //This Layer was assigned to GLA Normal Objects //Normal Layer
    private string fadeLayer = "FadeView"; //This Layer was assigned to GLA fading effect objects. //Glaucoma Layer 1
    private string glaucomaCopyLayer = "GLA2 Objects"; //Glaucoma Layer 2

    void Start()  
    {
        SetupCameraLayers();
    }

    public void SetupCameraLayers()
    {
        Debug.Log($"Culling setup for {TestEye}: Left {leftCamera.cullingMask}, Right {rightCamera.cullingMask}");

        if (leftCamera == null || rightCamera == null)
        {
            Debug.Log("Please Check the assigned Cameras.");
            return;
        }

        // Reset culling masks to default which shows all layers
        leftCamera.cullingMask = -1;
        rightCamera.cullingMask = -1;

        // Layer indexes
        int normalLayerIndex = LayerMask.NameToLayer(normalLayer);
        int fadeLayerIndex = LayerMask.NameToLayer(fadeLayer);
        int glaucomaCopyLayerIndex = LayerMask.NameToLayer(glaucomaCopyLayer);

        // Layer masks
        int normalLayerMask = 1 << normalLayerIndex;
        int fadeLayerMask = 1 << fadeLayerIndex;
        int glaucomaCopyLayerMask = 1 << glaucomaCopyLayerIndex;

        
                switch (TestEye)
                {
                    case ScotomaSimulator.Eye.Right:   //ScotomaSimulator.Eye.Right:
                        rightCamera.cullingMask &= ~normalLayerMask; //right sees the fade layer and left sees the normal layer
                        leftCamera.cullingMask &= ~fadeLayerMask;
                        break;
                    case ScotomaSimulator.Eye.Left:
                        rightCamera.cullingMask &= ~fadeLayerMask;  //right sees the normal layer and left sees the fade layer
                        leftCamera.cullingMask &= ~normalLayerMask;
                        break;
                    case ScotomaSimulator.Eye.Both: //Both cameras sees the fade view layer. we will use this case when both scotomas are activated.
                        leftCamera.cullingMask = -1;
                        rightCamera.cullingMask = -1;
                        //rightCamera.cullingMask &= ~glaucomaCopyLayerMask;  // Exclude GLA2 Objects from the right camera.
                        //leftCamera.cullingMask &= ~fadeLayerMask;  // Exclude FadeView from the left camera.
                        break;
                }

                Debug.Log($"Culling setup for {TestEye}: Left {leftCamera.cullingMask}, Right {rightCamera.cullingMask}");


    }
}
