using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class JumpingScript : MonoBehaviour
{
    private GameObject mainCamera = null;
    private GameObject platformCenter = null;
    private GameObject rightHandController = null;
    private XRController rightXRController = null;

    private Vector3 startPosition = Vector3.zero;
    private Quaternion startRotation = Quaternion.identity;
    private Quaternion rotTowardsHit = Quaternion.identity;

    public bool triggerPressed = false;
    public bool triggerReleased = false;
    private bool secondaryButtonLF = false;
    private Vector3 jumpingTargetPosition;
    private Vector3 centerOffset;

    private LineRenderer rightRayRenderer;
    private LineRenderer offsetRenderer;

    private bool rayOnFlag = false;

    public LayerMask myLayerMask;

    private GameObject rightRayIntersectionSphere = null;
    private GameObject jumpingPositionPreview = null;
    private GameObject jumpingPersonPreview = null;

    private RaycastHit hit;

    // YOUR CODE (IF NEEDED) - BEGIN 
    public bool triggerFullyPressed = false;
    // YOUR CODE - END    

    // Start is called before the first frame update
    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        mainCamera = GameObject.Find("Main Camera");
        platformCenter = GameObject.Find("Center");
        rightHandController = GameObject.Find("RightHand Controller");
        offsetRenderer = GetComponent<LineRenderer>();
        offsetRenderer.startWidth = 0.01f;
        offsetRenderer.positionCount = 2;

        if (rightHandController != null) // guard
        {
            rightXRController = rightHandController.GetComponent<XRController>();
            rightRayRenderer = rightHandController.AddComponent<LineRenderer>();
            rightRayRenderer.name = "Right Ray Renderer";
            rightRayRenderer.startWidth = 0.01f;
            rightRayRenderer.positionCount = 2;
            rayOnFlag = true;

            // geometry for intersection visualization
            rightRayIntersectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rightRayIntersectionSphere.name = "Right Ray Intersection Sphere";
            rightRayIntersectionSphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            rightRayIntersectionSphere.GetComponent<MeshRenderer>().material.color = Color.blue;
            rightRayIntersectionSphere.GetComponent<SphereCollider>().enabled = false; // disable for picking ?!
            rightRayIntersectionSphere.SetActive(false); // hide

            // geometry for Navidget visualization
            Material previewMaterial = new Material(Shader.Find("Standard"));
            previewMaterial.color = new Color(1.0f, 0.0f, 0.0f, 0.4f);
            previewMaterial.SetOverrideTag("RenderType", "Transparent");
            previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            previewMaterial.SetInt("_ZWrite", 0);
            previewMaterial.DisableKeyword("_ALPHATEST_ON");
            previewMaterial.DisableKeyword("_ALPHABLEND_ON");
            previewMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            previewMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            jumpingPositionPreview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            jumpingPositionPreview.transform.localScale = new Vector3(1f, 0.02f, 1f);
            jumpingPositionPreview.name = "Navidget Intersection Sphere";
            jumpingPositionPreview.layer = LayerMask.NameToLayer("Ignore Raycast");
            jumpingPositionPreview.GetComponent<MeshRenderer>().material = previewMaterial;
            jumpingPositionPreview.SetActive(false); // hide

            jumpingPersonPreview = Instantiate(Resources.Load("Prefabs/RealisticAvatar"), startPosition, startRotation) as GameObject;
            jumpingPersonPreview.layer = LayerMask.NameToLayer("Ignore Raycast");
            jumpingPersonPreview.SetActive(false);

            // YOUR CODE (IF NEEDED) - BEGIN 
            // YOUR CODE - END    

        }
    }

    // Update is called once per frame
    void Update()
    {
        UpdateOffsetToCenter();

        if (rightHandController != null) // guard
        {
            // mapping: grip button (middle finger)
            //float grip = 0.0f;
            //rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.grip, out grip);

            // mapping: joystick
            //Vector2 joystick;
            //rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out joystick);

            // mapping: primary button (A)
            //bool primaryButton = false;
            //rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButton);

            // mapping: trigger (index finger)
            float trigger = 0.0f;
            rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.trigger, out trigger);

            UpdateRayVisualization(trigger, 0.00001f);

            // YOUR CODE - BEGIN
            if (rayOnFlag && trigger >= 0.99999f)
            {
                if(!triggerFullyPressed)
                {
                    jumpingPositionPreview.SetActive(true);
                    jumpingPositionPreview.transform.position = rightRayIntersectionSphere.transform.position;
                    jumpingPersonPreview.SetActive(true);
                    jumpingPersonPreview.transform.position = new Vector3(rightRayIntersectionSphere.transform.position.x, mainCamera.transform.position.y, rightRayIntersectionSphere.transform.position.z);
                    jumpingTargetPosition = jumpingPositionPreview.transform.position;
                    triggerFullyPressed = true;
                }
            }

            if (trigger >= 0.00001f)
            {
                triggerPressed = true;

                Debug.Log(transform.position);
                Debug.Log(trigger);
            }
            if (triggerPressed && trigger >= 0.99999f)
            {
                jumpingPersonPreview.transform.LookAt(new Vector3(rightRayIntersectionSphere.transform.position.x, mainCamera.transform.position.y, rightRayIntersectionSphere.transform.position.z), Vector3.up);

            }
            if (triggerPressed && trigger <= 0.00001f)
            {
                transform.position = jumpingTargetPosition;
                transform.rotation = jumpingPersonPreview.transform.rotation;
                Debug.Log(transform.position);
                Debug.Log(trigger);

                triggerPressed = false;
            }
            if (!triggerPressed)
            {
                triggerFullyPressed = false;
            }
            // YOUR CODE - END    

            // mapping: secondary button (B)
            bool secondaryButton = false;
            rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButton);

            if (secondaryButton != secondaryButtonLF) // state changed
            {
                if (secondaryButton) // up (0->1)
                {
                    ResetXRRig();
                }
            }

            secondaryButtonLF = secondaryButton;
        }
    }

    private void UpdateOffsetToCenter()
    {
        // Calculate the offset between the platform center and the camera in the xz plane
        Vector3 a = transform.position;
        Vector3 b = new Vector3(mainCamera.transform.position.x, this.transform.position.y, mainCamera.transform.position.z);
        centerOffset = b - a;

        // visualize the offset as a line on the ground
        offsetRenderer.positionCount = 2; // line renderer visualizes a line between N (here 2) vertices
        offsetRenderer.SetPosition(0, a); // set pos 1
        offsetRenderer.SetPosition(1, b); // set pos 2

    }

    private void UpdateRayVisualization(float inputValue, float threshold)
    {
        // Visualize ray if input value is bigger than a certain treshhold
        if (inputValue > threshold && rayOnFlag == false)
        {
            rightRayRenderer.enabled = true;
            rayOnFlag = true;
        }
        else if (inputValue < threshold && rayOnFlag)
        {
            rightRayRenderer.enabled = false;
            rayOnFlag = false;
        }

        // update ray length and intersection point of ray
        if (rayOnFlag)
        { // if ray is on

            // Check if something is hit and set hit point
            if (Physics.Raycast(rightHandController.transform.position,
                                rightHandController.transform.TransformDirection(Vector3.forward),
                                out hit, Mathf.Infinity, myLayerMask))
            {
                rightRayRenderer.SetPosition(0, rightHandController.transform.position);
                rightRayRenderer.SetPosition(1, hit.point);

                rightRayIntersectionSphere.SetActive(true);
                rightRayIntersectionSphere.transform.position = hit.point;
            }
            else
            { // if nothing is hit set ray length to 100
                rightRayRenderer.SetPosition(0, rightHandController.transform.position);
                rightRayRenderer.SetPosition(1, rightHandController.transform.position + rightHandController.transform.TransformDirection(Vector3.forward) * 100);

                rightRayIntersectionSphere.SetActive(false);
            }
        }
        else
        {
            rightRayIntersectionSphere.SetActive(false);
        }
    }

    // YOUR CODE (ADDITIONAL FUNCTIONS)- BEGIN
        
    // YOUR CODE - END 

    private void ResetXRRig()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;
    }
}
