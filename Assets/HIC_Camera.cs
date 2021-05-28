using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Syy.Tools.GameViewSizeTool;


public class HIC_Camera : MonoBehaviour
{

    public IDictionary<string, int> classes = new Dictionary<string, int>()
                                            {
                                                {"Car", 0}
                                            }; //custom for BDD


    public float focalLength = 1.93f;
    public float baseline = 55; //in millimeters
    public float verticalFOV = 65.5f;
    public float sensorWidth = 3.896f;
    public float sensorHeight = 2.453f;

    //intel D435
    //focal length = 1.93mm
    //vFoV = 65.5
    //ov9282 sensor is 3.896mm x 2.453mm

    public enum motions {Static, Linear, Oscillate};
    public motions movementMode;

    public Transform cameraChassis;

    public float oscillationRadius = 0.117094f; //meters
    public float oscillationAngularSpeed = 90; //degrees per second

    public float linearRange = 0.2794f; //meters
    public float linearSpeed = 0.2032f; //meters per second

    private float framePeriod;
    private float timeSinceStart;

    private DirectoryInfo leftCamDir;
    private DirectoryInfo rightCamDir;

    public bool toggleCameraCapture = false;
    public bool toggleDepthCapture = false;
    public float fixedFPS = 60;

    public bool toggleBoundingBoxes = false;

    public int captureWidth = 1200;
    public int captureHeight = 800;

    private int fixedUpdateIterations;

    Camera leftCamera;
    Camera rightCamera;

    public int captureFrames = 100;

    public int startDelayFrames = 0;

    public float cameraBoundingBoxDistance = 20f;

    public float labellingPad = 0.1f; //padding around screen space to threshold if label will be YOLO valid.

    public bool toggleYOLOFormatRight = false;

    public bool toggleRootObjectBoundingBox = false;
    void Start()
    {


        fixedUpdateIterations = 0;

        if (toggleCameraCapture == true)
        {
            leftCamDir = Directory.CreateDirectory(string.Format("Session_{0:yyyy-MM-dd}", DateTime.Now) + "_" + "left" + "_" + gameObject.name);
            rightCamDir = Directory.CreateDirectory(string.Format("Session_{0:yyyy-MM-dd}", DateTime.Now) + "_" + "right" + "_" + gameObject.name);
        }

        if (toggleCameraCapture == true)
        {
            CreateClassesTextFile(leftCamDir);
            CreateClassesTextFile(rightCamDir);
        }

        Time.fixedDeltaTime = 1 / fixedFPS;

        timeSinceStart = 0f;

        framePeriod = Time.fixedDeltaTime;

        baseline = baseline / 1000f; //convert mm to meters - unity world units


        cameraChassis = transform.GetChild(0);

        List<Camera> cameras = GetComponentsInChildren<Camera>().ToList();

        foreach (Camera mono_cam in cameras)
        {
            mono_cam.usePhysicalProperties = true;
            mono_cam.sensorSize = new Vector2(sensorWidth, sensorHeight);
            mono_cam.focalLength = focalLength;
            mono_cam.fieldOfView = verticalFOV;

            if (mono_cam.name == "Right Cam")
            {
                mono_cam.transform.localPosition = new Vector3(baseline / 2f, 0f, 0f);

                rightCamera = mono_cam;

                if (toggleDepthCapture == true)
                {
                    rightCamera.GetComponent<ImageSynthesis>().toggleDepth = true;
                }
                else
                {
                    rightCamera.GetComponent<ImageSynthesis>().toggleDepth = false;
                }
            }

            if (mono_cam.name == "Left Cam")
            {
                mono_cam.transform.localPosition = new Vector3(-baseline / 2f, 0f, 0f);

                leftCamera = mono_cam;

                if (toggleDepthCapture == true)
                {
                    leftCamera.GetComponent<ImageSynthesis>().toggleDepth = true;
                }
                else
                {
                    leftCamera.GetComponent<ImageSynthesis>().toggleDepth = false;
                }
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        
        switch ((int)movementMode)
        {
            case 0: //stat
                break;

            case 1: //lin
                float period = ((linearRange / 2f) / linearSpeed) * 4f;
                cameraChassis.localPosition = new Vector3(TriangleWave(timeSinceStart, linearRange / 2f, period), 0f, 0f);
                break;

            case 2: //osc
                float timeParameter = (3 * Mathf.PI) / 2 + (TriangleWave(timeSinceStart * oscillationAngularSpeed * Mathf.Deg2Rad, Mathf.PI / 2f, (Mathf.PI / 2f) * 4f)); //goes between pi and 2pi
                Vector3 localPositionSemiCircle = new Vector3(oscillationRadius * Mathf.Cos(timeParameter), oscillationRadius * Mathf.Sin(timeParameter), 0f);
                cameraChassis.localPosition = localPositionSemiCircle;
                break;
        }

        SaveImageAndBoundingBoxes(cameraChassis, leftCamera, cameraBoundingBoxDistance, leftCamDir, fixedUpdateIterations, captureWidth, captureHeight, 0);
        SaveImageAndBoundingBoxes(cameraChassis, rightCamera, cameraBoundingBoxDistance, rightCamDir, fixedUpdateIterations, captureWidth, captureHeight, 0);




        timeSinceStart = timeSinceStart + framePeriod;

        fixedUpdateIterations++;

        if (toggleCameraCapture == true && fixedUpdateIterations == captureFrames)
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }

        
    }



    public void SaveImageAndBoundingBoxes(Transform cameraChassis, Camera targetCamera, float cameraBoundingBoxDistance, DirectoryInfo cameraDirectory, int frameNumber, int captureWidth, int captureHeight, int frameNumberOffset = 0)
    {
        Collider[] hitColliders = Physics.OverlapSphere(cameraChassis.position, cameraBoundingBoxDistance, ~0, QueryTriggerInteraction.Collide);
        
        List<RootObject> rootObjects = new List<RootObject>();

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.gameObject.GetComponent<RootObject>() != null)
            {
                Vector3 screenPoint = targetCamera.WorldToViewportPoint(hitCollider.transform.position);
                bool onScreen = screenPoint.z >= 0f && screenPoint.x > 0f && screenPoint.x < 1f && screenPoint.y > 0f && screenPoint.y < 1f;

                if (onScreen)
                {
                    rootObjects.Add(hitCollider.gameObject.GetComponent<RootObject>());
                }
            }
        }


        using (StreamWriter streamWriter = File.CreateText(cameraDirectory.FullName + "/" + frameNumber.ToString().PadLeft(10, '0') + ".txt"))
        {
            foreach (RootObject rootObject in rootObjects)
            {

                Vector3[] pts3D = new Vector3[8];

                BoxCollider col = rootObject.gameObject.GetComponent<BoxCollider>();

                var trans = col.transform;
                var min_ = col.center - col.size * 0.5f;
                var max_ = col.center + col.size * 0.5f;

                Bounds b = col.bounds;

                pts3D[0] = trans.TransformPoint(new Vector3(min_.x, min_.y, min_.z));
                pts3D[1] = trans.TransformPoint(new Vector3(min_.x, min_.y, max_.z));
                pts3D[2] = trans.TransformPoint(new Vector3(min_.x, max_.y, min_.z));
                pts3D[3] = trans.TransformPoint(new Vector3(min_.x, max_.y, max_.z));
                pts3D[4] = trans.TransformPoint(new Vector3(max_.x, min_.y, min_.z));
                pts3D[5] = trans.TransformPoint(new Vector3(max_.x, min_.y, max_.z));
                pts3D[6] = trans.TransformPoint(new Vector3(max_.x, max_.y, min_.z));
                pts3D[7] = trans.TransformPoint(new Vector3(max_.x, max_.y, max_.z));


                string className = rootObject.className;

                bool colliderRayCastSuccessful = false;

                foreach (Vector3 corner in pts3D) //raycast to the 8 corners of the box to see if it is occluded fully
                {
                    if (colliderRayCastSuccessful == true)
                    {
                        break;
                    }

                    RaycastHit hit;

                    if (Physics.Raycast(cameraChassis.position, corner - cameraChassis.position, out hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    {
                        if (hit.collider == rootObject.gameObject.GetComponent<BoxCollider>()) //if one of the corners are hit, that means object is at least partially visible
                        {
                            colliderRayCastSuccessful = true;
                        }
                    }

                }

                if (colliderRayCastSuccessful == false)
                {
                    //continue; //that means object is occluded so move onto next rootobject
                }

                for (int i = 0; i < pts3D.Length; i++)
                {
                    pts3D[i] = targetCamera.WorldToScreenPoint(pts3D[i]); //convert global space box corners to camera space
                }

                Vector3 min = pts3D[0];
                Vector3 max = pts3D[0];

                for (int i = 1; i < pts3D.Length; i++)
                {
                    min = Vector3.Min(min, pts3D[i]);
                    max = Vector3.Max(max, pts3D[i]);
                }

                min.y = captureHeight - min.y;
                max.y = captureHeight - max.y; //changing direction of y axis

                //Construct a rect of the min and max positions and apply some margin

                Rect r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                float XCoord = (r.xMin + r.xMax) / 2;
                float YCoord = (r.yMin + r.yMax) / 2; // x and y center in screen space

                float height = Mathf.Abs(r.yMin - r.yMax);
                float width = Mathf.Abs(r.xMin - r.xMax); //height and width of box in screen space

                float x1 = XCoord - width / 2f;
                float y1 = YCoord - height / 2f;

                float x2 = XCoord + width / 2f;
                float y2 = YCoord + height / 2f;

                float x3 = 0f;
                float y3 = 0f;

                float x4 = captureWidth;
                float y4 = captureHeight;

                float x5 = Mathf.Max(x1, x3);
                float y5 = Mathf.Max(y1, y3);

                float x6 = Mathf.Min(x2, x4);
                float y6 = Mathf.Min(y2, y4);

                XCoord = (x5 + x6) / 2f;
                YCoord = (y5 + y6) / 2f;

                width = x6 - x5;
                height = y6 - y5;

                if (width <= 0 || height <= 0)
                {
                    continue;
                }

                if (toggleYOLOFormatRight == true)
                {
                    XCoord = XCoord / captureWidth;
                    YCoord = YCoord / captureHeight;
                    height = height / captureHeight;
                    width = width / captureWidth; //normalize ground truth to screen dimensions for YOLOv3 labelling format
                }

                else
                {
                    XCoord = XCoord - (width / 2f);
                    YCoord = YCoord - (height / 2f);
                }

                if (classes.ContainsKey(className))
                {
                    streamWriter.WriteLine(String.Format("{0} {1} {2} {3} {4}", classes[className], XCoord, YCoord, width, height));
                } //checks if center of box is within specified padded area. prevents bad labels (negative centers)
            }
        }

        targetCamera.GetComponent<ImageSynthesis>().Save((frameNumber + frameNumberOffset).ToString().PadLeft(10, '0'), captureWidth, captureHeight, cameraDirectory.ToString());

    }
    public float TriangleWave(float x, float a, float p)
    {
        return ((4f * a) / p) * UnityEngine.Mathf.Abs(((x - p / 4f) % p) - p / 2f) - a;
    }

    private void CreateClassesTextFile(DirectoryInfo datasetDir)
    {
        using (StreamWriter sw = File.CreateText(datasetDir.FullName + "/classes.txt"))
        {
            int i = 0;

            foreach (KeyValuePair<string, int> item in classes)
            {
                while (item.Value != i)
                {
                    i++;
                    sw.WriteLine("");
                }

                sw.WriteLine(item.Key);
                i++;
            }
        }
    }

    public Vector3[] CalculateBoundingBox(GameObject aObj, Camera camera)
    {
        Transform myTransform = aObj.transform;
        Mesh mesh = null;
        MeshFilter mF = aObj.GetComponent<MeshFilter>();
        if (mF != null)
            mesh = mF.mesh;
        else
        {
            SkinnedMeshRenderer sMR = aObj.GetComponent<SkinnedMeshRenderer>();
            if (sMR != null)
                mesh = sMR.sharedMesh;
        }
        if (mesh == null)
        {
            Debug.LogError(" no mesh found on the given object");
            return null;
        }
        Vector3[] vertices = mesh.vertices;
        if (vertices.Length <= 0)
        {
            Debug.LogError("mesh doesn't have vertices");
            return null;
        }
        Vector3 min, max;
        //convert to world
        min = max = myTransform.TransformPoint(vertices[0]);
        // convert to screen
        min = max = camera.WorldToScreenPoint(min);
        for (int i = 1; i < vertices.Length; i++)
        {

            vertices[i].y = vertices[i].y - 0.1f;

            Vector3 V = myTransform.TransformPoint(vertices[i]);
            V = camera.WorldToScreenPoint(V);
            for (int n = 0; n < 2; n++)
            {
                if (V[n] > max[n])
                    max[n] = V[n];
                if (V[n] < min[n])
                    min[n] = V[n];
            }
        }
        //Bounds B = new Bounds();
        //B.SetMinMax(min, max);
        min[1] = Screen.height - min[1];
        max[1] = Screen.height - max[1];
        Vector3 point1 = min;
        Vector3 point2 = max;

        Vector3[] returnArray = new Vector3[] { min, max };

        return returnArray;
    }


}
