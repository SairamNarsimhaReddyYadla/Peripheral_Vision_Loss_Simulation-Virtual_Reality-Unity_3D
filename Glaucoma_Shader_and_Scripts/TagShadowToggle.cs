//Code to simulate the shadow visibilty of objects.


using UnityEngine;


//It is also better to check if the lights in the rooms are activated to give shadows.Otherwise objects are not casting the shadows.

public class TagShadowToggle : MonoBehaviour //Toggle the shadows of the object that has the Tag assigned.
{
    public Camera observingCamera;
    public Transform sphereCenter; // Reference to the sphere's transform (It is using sphere collider radius not mesh radius :) )

    //In VR laser script, this collider will be ignored (with Tag IgnoreScotomaSphereCollison).So that the VR rays don't interact with the sphere.

    public float searchSphereRadius = 1.0f; //To check within this radius instead of infinity.
    public string targetTag = "GLATargetObject"; // Tag to identify target glaucoma objects in the scene
    public Color rayColor = Color.yellow; // Color of the debug ray in the Scene view
    public Color hitColor = Color.red; // Color of the hit marker

    void Update()
    {
        if (observingCamera == null || sphereCenter == null) return;

        // Find all objects with the specified tag
        GameObject[] targetObjects = GameObject.FindGameObjectsWithTag(targetTag);

        foreach (GameObject targetObject in targetObjects)
        {
            if (targetObject == null) continue;

            Vector3 directionToTarget = targetObject.transform.position - observingCamera.transform.position;
            // Using searchSphereRadius for visibility checks within the defined radius.
            RaycastHit hit;

            if (Physics.Raycast(observingCamera.transform.position, directionToTarget.normalized, out hit))
            {
                Debug.DrawLine(observingCamera.transform.position, hit.point, rayColor);
                bool isObstructedBySphere = hit.transform == sphereCenter;

                Debug.DrawRay(hit.point, Vector3.up * 0.5f, hitColor);

                bool isInsideSphere = isObstructedBySphere && (hit.distance <= searchSphereRadius);
                ToggleShadows(targetObject, isInsideSphere);
            }
            else
            {
                ToggleShadows(targetObject, true);
            }
        }
    }

    private void ToggleShadows(GameObject obj, bool isVisible)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Toggle shadow casting mode based on visibility
            renderer.shadowCastingMode = isVisible ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
