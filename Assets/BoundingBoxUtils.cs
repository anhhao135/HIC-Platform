using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BoundingBoxUtils : MonoBehaviour
{
    // Start is called before the first frame update
    private void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
    }

    static public void SaveImageAndBoundingBoxes(Transform cameraChassis, Camera targetCamera, float cameraBoundingBoxDistance, DirectoryInfo cameraDirectory, int frameNumber, int captureWidth, int captureHeight, IDictionary<string, int> classes, bool toggleYOLOFormatRight, float bboxOcclusionRatio, int frameNumberOffset = 0)
    {
        Collider[] hitColliders = Physics.OverlapSphere(cameraChassis.position, cameraBoundingBoxDistance);

        List<RootObject> rootObjects = new List<RootObject>();

        foreach (Collider hitCollider in hitColliders)
        {
            RootObject rootObject = hitCollider.gameObject.GetComponent<RootObject>();

            if (rootObject != null)
            {
                Vector3 screenPoint = targetCamera.WorldToViewportPoint(hitCollider.transform.position);
                bool onScreen = screenPoint.z >= -0.5f && screenPoint.x > 0f && screenPoint.x < 1f && screenPoint.y > 0f && screenPoint.y < 1f;
                onScreen = true;

                if (onScreen)
                {
                    if (Vector3.Magnitude(rootObject.transform.position - cameraChassis.position) < cameraBoundingBoxDistance)
                    {
                        rootObjects.Add(rootObject);
                    }
                }
            }
        }


        using (StreamWriter streamWriter = File.CreateText(cameraDirectory.FullName + "/Annotations/" + frameNumber.ToString().PadLeft(10, '0') + ".txt"))
        {
            foreach (RootObject rootObject in rootObjects)
            {
                string className = rootObject.className;

                /*
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
                    continue; //that means object is occluded so move onto next rootobject
                }

                */

                try
                {
                    Vector3[] minMaxPoints = CalculateBoundingBox(rootObject.gameObject, targetCamera);

                    Vector3 min = minMaxPoints[0];
                    Vector3 max = minMaxPoints[1];

                    /*

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

                    */

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
                    float y6 = Mathf.Min(y2, y4); //clip the box to the screen viewing space

                    XCoord = (x5 + x6) / 2f;
                    YCoord = (y5 + y6) / 2f;

                    width = x6 - x5;
                    height = y6 - y5;

                    if (width <= 0 || height <= 0 || width * height > 0.5f * captureHeight * captureHeight)
                    {
                        continue; //discard if box is out of screen
                    }

                    //XCoord and YCoord is corner of box. X is pointing right. Y is pointing down.
                    //Width and height are dimensions of box
                    //All are in terms of pixels

                    if (toggleYOLOFormatRight == true)
                    {
                        XCoord = XCoord;// / captureWidth;
                        YCoord = YCoord;// / captureHeight;
                        height = height;// / captureHeight;
                        width = width;// / captureWidth; //normalize ground truth to screen dimensions for YOLOv3 labelling format
                    }
                    else
                    {
                        XCoord = XCoord - (width / 2f);
                        YCoord = YCoord - (height / 2f);
                    }

                    float boxArea = width * height;

                    float segmentArea = getSegmentArea(rootObject.gameObject.transform.root.gameObject.GetInstanceID(), targetCamera);

                    if (segmentArea == 0 || boxArea == 0)
                    {
                        continue;
                    }
                    else
                    {
                        float ratio = segmentArea / boxArea;


                        if (classes.ContainsKey(className) && ratio > bboxOcclusionRatio) //if class is not defined, then do not save label
                        {
                            streamWriter.WriteLine(String.Format("{0} {1} {2} {3} {4}", classes[className], XCoord, YCoord, width, height));
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        targetCamera.GetComponent<ImageSynthesis>().Save((frameNumber + frameNumberOffset).ToString().PadLeft(10, '0'), captureWidth, captureHeight, Path.Combine(cameraDirectory.ToString(), "Img"));
    }

    private static float getSegmentArea(int instanceID, Camera targetCamera)
    {
        ImageSynthesis targetIS = targetCamera.GetComponent<ImageSynthesis>();

        Color targetColor = targetIS.instanceSegDictColor[instanceID];
        Color[] segmentImage = targetIS.GetSegmentationPixels();

        int matchCount = 0;

        foreach (Color pixelColor in segmentImage)
        {
            if (pixelColor == targetColor)
            {
                matchCount++;
            }
        }

        return matchCount;
    }

    static public void getWorldMesh(Transform obj, List<Vector3> worldMesh, Camera targetCamera)
    {
        Mesh mesh = null;
        MeshFilter mF = obj.GetComponent<MeshFilter>();
        if (mF != null)
            mesh = mF.mesh;
        else
        {
            SkinnedMeshRenderer sMR = obj.GetComponent<SkinnedMeshRenderer>();
            if (sMR != null)
                mesh = sMR.sharedMesh;
        }

        if (mesh == null)
        {
            foreach (Transform child in obj)
            {
                getWorldMesh(child, worldMesh, targetCamera);
            }

            return;
        }

        Vector3[] vertices = mesh.vertices;

        if (vertices.Length <= 0)
        {
            foreach (Transform child in obj)
            {
                getWorldMesh(child, worldMesh, targetCamera);
            }

            return;
        }

        foreach (Vector3 vert in vertices)
        {
            Vector3 globalPoint = obj.TransformPoint(vert);

            Vector3 localScreenSpace = targetCamera.WorldToViewportPoint(globalPoint);

            if (localScreenSpace.x >= 0 && localScreenSpace.x <= 1 && localScreenSpace.y >= 0 && localScreenSpace.y <= 1 && localScreenSpace.z > 0)
            {
                worldMesh.Add(globalPoint);
            }
        }

        foreach (Transform child in obj)
        {
            getWorldMesh(child, worldMesh, targetCamera);
        }
    }

    static public Vector3[] CalculateBoundingBox(GameObject aObj, Camera cam)
    {
        Transform parent = aObj.transform.root;
        List<Vector3> worldMesh = new List<Vector3>();
        getWorldMesh(parent, worldMesh, cam);

        Vector3 min, max;
        min = max = cam.WorldToScreenPoint(worldMesh[0]);
        for (int i = 1; i < worldMesh.Count; i++)
        {
            Vector3 screenPoint = cam.WorldToScreenPoint(worldMesh[i]);
            for (int n = 0; n < 2; n++)
            {
                if (screenPoint[n] > max[n])
                    max[n] = screenPoint[n];
                if (screenPoint[n] < min[n])
                    min[n] = screenPoint[n];
            }
        }

        Vector3[] returnPoints = { min, max };

        return returnPoints;
    }
}