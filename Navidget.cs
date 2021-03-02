using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class Navidget : MonoBehaviour
{

    private GameObject mainCamera = null;
    private GameObject rightHandController = null;
    private XRController rightXRController = null;

    public float translationFactor = 1.0f;
    public float rotationFactor = 1.0f;

    private Vector3 startPosition = Vector3.zero;//new Vector3(70.28f, 22.26f, 37.78f);
    private Quaternion startRotation = Quaternion.identity; //Vector3(0,312.894073,0)

    private bool primaryButtonLF = false;
    private bool secondaryButtonLF = false;

    private LineRenderer rightRayRenderer;

    public LayerMask myLayerMask;

    private GameObject rightRayIntersectionSphere = null;
    private GameObject navidgetIntersectionSphere = null;
    private GameObject navidgetDirectionStick = null;
    private GameObject navidgetPreviewPose = null;

    private RaycastHit rightHit;

    private bool animationFlag = false;
    public float animationDuration = 3.0f; // in seconds
    private float t;


    // Start is called before the first frame update
    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        mainCamera = GameObject.Find("Main Camera");
        rightHandController = GameObject.Find("RightHand Controller");

        if (rightHandController != null) // guard
        {
            rightXRController = rightHandController.GetComponent<XRController>();

            //rightRayRenderer = gameObject.AddComponent<LineRenderer>();
            rightRayRenderer = rightHandController.AddComponent<LineRenderer>();
            //rightRayRenderer = rightHandController.GetComponent<LineRenderer>();
            rightRayRenderer.name = "Right Ray Renderer";
            rightRayRenderer.startWidth = 0.01f;
            rightRayRenderer.positionCount = 2; // two points (one line segment)

            // geometry for intersection visualization
            rightRayIntersectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //rightRayIntersectionSphere.transform.parent = this.gameObject.transform;
            rightRayIntersectionSphere.name = "Right Ray Intersection Sphere";
            rightRayIntersectionSphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            rightRayIntersectionSphere.GetComponent<MeshRenderer>().material.color = Color.blue;
            rightRayIntersectionSphere.GetComponent<SphereCollider>().enabled = false; // disable for picking ?!
            rightRayIntersectionSphere.SetActive(false); // hide

            // geometry for Navidget visualization
            Material navidgetSphereMaterial = new Material(Shader.Find("Standard"));
            navidgetSphereMaterial.color = new Color(1.0f, 0.0f, 0.0f, 0.4f);
            navidgetSphereMaterial.SetOverrideTag("RenderType", "Transparent");
            //SetupMaterialWithBlendMode(navidgetSphereMaterial, BlendMode.Transparent);
            navidgetSphereMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            navidgetSphereMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            navidgetSphereMaterial.SetInt("_ZWrite", 0);
            navidgetSphereMaterial.DisableKeyword("_ALPHATEST_ON");
            navidgetSphereMaterial.DisableKeyword("_ALPHABLEND_ON");
            navidgetSphereMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            navidgetSphereMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            navidgetIntersectionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            navidgetIntersectionSphere.name = "Navidget Intersection Sphere";
            navidgetIntersectionSphere.GetComponent<MeshRenderer>().material = navidgetSphereMaterial;
            //navidgetIntersectionSphere.GetComponent<SphereCollider>().enabled = false; // disable for picking ?!
            navidgetIntersectionSphere.SetActive(false); // hide


            navidgetDirectionStick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            navidgetDirectionStick.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
            navidgetDirectionStick.name = "Navidget Direction Stick";
            navidgetDirectionStick.GetComponent<MeshRenderer>().material.color = Color.blue;
            navidgetDirectionStick.GetComponent<CapsuleCollider>().enabled = false; // disable for picking ?!
            navidgetDirectionStick.SetActive(false); // hide

            navidgetPreviewPose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            navidgetPreviewPose.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            navidgetPreviewPose.name = "Navidget Preview Pose";
            navidgetPreviewPose.GetComponent<MeshRenderer>().material.color = Color.red;
            navidgetPreviewPose.GetComponent<BoxCollider>().enabled = false; // disable for picking ?!
            navidgetPreviewPose.SetActive(false); // hide
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (rightHandController != null) // guard
        {

            // ----------------- Steering stuff -----------------

            // mapping: grip button (middle finger)
            float grip = 0.0f;
            rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.grip, out grip);
            //Debug.Log("middle finger rocker: " + grip);

            // mapping: trigger (index finger)
            float trigger = 0.0f;
            rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.trigger, out trigger);
            //Debug.Log("index finger rocker: " + trigger);

            steering(0.0f, 0.0f, trigger, 0.0f, 0.0f, 0.0f); // map as forward steering input

            // mapping: joystick
            Vector2 joystick;
            rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out joystick);

            //steering(joystick.x, 0.0f, joystick.y, 0.0f, 0.0f, 0.0f); // map vertical jostick axis as forward/backward input
            //steering(0.0f, 0.0f, joystick.y, joystick.x, 0.0f, 0.0f);
            steering(0.0f, 0.0f, 0.0f, joystick.y, joystick.x, 0.0f); // map as head & pitch rotation

            // ----------------- NAVIDGET stuff -----------------

            // mapping: primary button (A)
            bool primaryButton = false;
            rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButton);
            //Debug.Log("primary button: " + primaryButton);
            
            if (primaryButton != primaryButtonLF) // state changed
            {
                if (primaryButton) // up (false -> true)
                {
                    if (rightHit.collider != null) // something was hit
                    {
                        // start target pose specification

                        // YOUR CODE - BEGIN
                        navidgetIntersectionSphere.SetActive(true);
                        navidgetDirectionStick.SetActive(true);                    
                        navidgetPreviewPose.SetActive(true);
                        navidgetIntersectionSphere.transform.position = rightRayIntersectionSphere.transform.position;
                        // YOUR CODE - END
                    }

                }
                else // down (true -> false)
                {
                    // YOUR CODE - BEGIN

                    // finish target pose specification and start animation process
                    navidgetIntersectionSphere.SetActive(false);
                    navidgetDirectionStick.SetActive(false);
                    t = animationDuration;
                    animationFlag = true;
                    // YOUR CODE - END
                }
            }
            else
            {
                if (primaryButton) // high (true -> true)
                {
                    // YOUR CODE - BEGIN
                    float distance = Vector3.Distance(rightHit.point, navidgetIntersectionSphere.transform.position);

                    Vector3 relativePos = Vector3.Normalize(rightHit.point - navidgetIntersectionSphere.transform.position);
                    Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.down);

                    Vector3 pos = Vector3.Normalize(navidgetDirectionStick.transform.position - navidgetIntersectionSphere.transform.position);
                    Quaternion rotation1 = Quaternion.LookRotation(relativePos, Vector3.forward);                 
                    navidgetPreviewPose.transform.position = navidgetDirectionStick.transform.position + 1.05f * pos * distance;


                    // for being able to teleport behind the sphere --> comment if statement
                    if (rightHit.collider.GetType() == typeof(SphereCollider))
                    {
                        navidgetDirectionStick.transform.rotation = rotation * Quaternion.Euler(-90, 0, 0);
                        navidgetDirectionStick.transform.position = navidgetIntersectionSphere.transform.position + 0.5f * relativePos;
                        navidgetPreviewPose.transform.rotation = rotation * Quaternion.Euler(-90, 0, 0);
                    }
                        
                    // update target pose (position and orientation) with respect to point of interest

                    /*if (rightHit.collider != null && rightHit.collider.GetType() == typeof(SphereCollider))
                    {
                        navidgetDirectionStick.transform.LookAt(navidgetIntersectionSphere.transform.position, Vector3.right);
                        navidgetPreviewPose.transform.position = navidgetIntersectionSphere.transform.position + 1.05f * relativePos;
                    } */
                    // YOUR CODE - END
                }
            }

            primaryButtonLF = primaryButton;

            // ----------------- Reset stuff -----------------

            // mapping: secondary button (B)
            bool secondaryButton = false;
            rightXRController.inputDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButton);
            //Debug.Log("secondary button: " + secondaryButton);

            if (secondaryButton != secondaryButtonLF) // state changed
            {
                if (secondaryButton) // up (0->1)
                {
                    resetXRRig();
                }
            }

            secondaryButtonLF = secondaryButton;

            animateXRRig();
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // ray intersection

        // Does the ray intersect any objects
        if (Physics.Raycast(rightHandController.transform.position, rightHandController.transform.TransformDirection(Vector3.forward), out rightHit, Mathf.Infinity, myLayerMask))
        {
            //Debug.Log("Did Hit");
            // update ray visualization
            rightRayRenderer.SetPosition(0, rightHandController.transform.position);
            rightRayRenderer.SetPosition(1, rightHit.point);

            // update intersection sphere visualization
            rightRayIntersectionSphere.SetActive(true); // show
            rightRayIntersectionSphere.transform.position = rightHit.point;
        }
        else
        {
            //Debug.Log("Did not Hit");
            // update ray visualization
            rightRayRenderer.SetPosition(0, rightHandController.transform.position);
            rightRayRenderer.SetPosition(1, rightHandController.transform.position + rightHandController.transform.TransformDirection(Vector3.forward) * 1000);

            // update intersection sphere visualization
            rightRayIntersectionSphere.SetActive(false); // hide
        }   
    }

    private void animateXRRig()
    {
        // YOUR CODE - BEGIN
        
        // interpolate position -> Vector3.Lerp()
        if (animationFlag)
        {
            float rate = (float) (animationDuration - t) / animationDuration;
            
            if (t < 0)
            {
                animationFlag = false;
                
            }
            Vector3 relativePos = Vector3.Normalize(navidgetDirectionStick.transform.position - navidgetIntersectionSphere.transform.position);
            Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.down);


            transform.position = Vector3.Lerp(transform.position, navidgetPreviewPose.transform.position, rate);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation * Quaternion.Euler(-180, 0, 0), rate);

            t -= Time.deltaTime;
            navidgetPreviewPose.SetActive(false);

            /*
            if (transform.position == navidgetDirectionStick.transform.position)
             {                             
                 t = 3.0f;          
             }
            */
        }
        // interpolate rotation -> Quaternion.Slerp()
        //Debug.Log(animationFlag);
        // YOUR CODE - END        
    }

    private void steering(float X, float Y, float Z, float RX, float RY, float RZ)
    {
        if (animationFlag) return; // abort steering when in animation process

        // translation in absolute controller direction
        Matrix4x4 rotmat = Matrix4x4.Rotate(rightHandController.transform.rotation);
        Matrix4x4 rotmatRig = Matrix4x4.Rotate(transform.rotation);
        Vector4 moveVec = new Vector4(X, Y, Z, 0.0f);
        float length = moveVec.magnitude;
        moveVec.Normalize();
        moveVec = moveVec * Mathf.Pow(length, 3); // exponential transfer function

        moveVec = rotmatRig.inverse * rotmat * moveVec;
        transform.Translate(moveVec * Time.deltaTime * translationFactor); // accumulate translation input


        // Head rotation
        RY = Mathf.Pow(RY, 3); // exponential transfer function
        //transform.Rotate(0.0f, RY * Time.deltaTime * rotationFactor, 0.0f, Space.Self); // rotate around platform center
        transform.RotateAround(mainCamera.transform.position, Vector3.up, RY * Time.deltaTime * rotationFactor); // rotate arround camera position (on platform)

        // Pitch rotation
        RX = Mathf.Pow(RX, 3); // exponential transfer function
        //transform.Rotate(RX * Time.deltaTime * rotationFactor, 0.0f, 0.0f, Space.Self); // rotate around platform center
        //transform.RotateAround(mainCamera.transform.position, Vector3.right, RX * Time.deltaTime * rotationFactor); // rotate arround camera position (on platform)
        Vector3 rightLocal = transform.TransformDirection(Vector3.right);
        transform.RotateAround(mainCamera.transform.position, rightLocal, RX * Time.deltaTime * rotationFactor); // rotate arround camera position (on platform) in local platform coordinate system

        // Roll rotation not implemented
    }


    private void resetXRRig()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;

        animationFlag = false;
    }
}
