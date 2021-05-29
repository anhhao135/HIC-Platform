using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Generation : MonoBehaviour
{
    public List<GameObject> cars;
    public Transform carSpawnPointsRoot;
    private List<Transform> carSpawnPoints;
    public float fixedUpdateFrequency = 50; //in Hz
    public List<GameObject> spawnedCars;
    public float randomRotationAmount = 10f;
    public float randomPositionAmount = 0.5f;
    public Camera generationCamera;

    public float cameraBoundingBoxDistance = 20f;

    public bool toggleRandomCarColors = false;
    public float randomCarColorChance;
    public bool toggleRandomSunDirection = false;

    public float randomSunDeviation = 30f;
    /*
    public IDictionary<string, int> classes = new Dictionary<string, int>()
                                            {
                                                {"Person", 0},
                                                {"Bicycle", 1},
                                                {"Car", 2},
                                                {"Motorbike", 3},
                                                {"Bus", 4},
                                                {"Truck", 5},
                                                {"Traffic Light", 6},
                                                {"Fire Hydrant", 7},
                                                {"Bench", 8},
                                                {"Umbrella", 9},
                                                {"Potted Plant", 10},
                                                {"Traffic Sign", 11},
                                                {"Train", 12}
                                            }; //custom for BDD

    */

    public IDictionary<string, int> classes = new Dictionary<string, int>()
                                            {
                                                {"Car", 0}
                                            }; //custom for BDD

    private DirectoryInfo dataset;

    private string nameTime;

    public float labellingPad = 0.1f; //padding around screen space to threshold if label will be YOLO valid.

    private int fixedUpdateCount;

    private ImageSynthesis IS;

    public int captureHeight = 1080;
    public int captureWidth = 1920;

    RenderTexture camRenderTexture;

    public float carSpawnChance = 0.5f;

    public float randomCameraRotationAmount = 10f;
    public float randomCameraPositionAmount = 0.5f;
    public float randomCameraRotationAmountY = 90f;

    public float relevantRadiusToCamera = 10f;

    public int captureNumber;

    public Transform sun;

    // Start is called before the first frame update
    private void Start()
    {
        camRenderTexture = new RenderTexture(captureWidth, captureHeight, 16, RenderTextureFormat.ARGB32);
        camRenderTexture.Create();
        generationCamera.targetTexture = camRenderTexture;

        IS = generationCamera.GetComponent<ImageSynthesis>();

        fixedUpdateCount = 0;

        nameTime = DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss-tt");

        dataset = Directory.CreateDirectory(string.Format("DataSet_{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now)); //create dataset directory

        carSpawnPoints = new List<Transform>();

        foreach (Transform carSpawnPoint in carSpawnPointsRoot)
        {
            carSpawnPoints.Add(carSpawnPoint);

            carSpawnPoint.position += carSpawnPoint.up * 1f;

            RaycastHit hit;

            if (Physics.Raycast(carSpawnPoint.position, -carSpawnPoint.up, out hit, Mathf.Infinity))
            {
                
                Vector3 originalFrontLookVectorWorldSpace = carSpawnPoint.forward;
                Vector3 hitNormalWorldSpace = hit.normal;
                Vector3 hitPointWorldSpace = hit.point;
                Quaternion rotationAdjustment = Quaternion.FromToRotation(carSpawnPoint.up, hitNormalWorldSpace);

                carSpawnPoint.position = hitPointWorldSpace;
                carSpawnPoint.rotation = rotationAdjustment;

                Vector3 lookDirectionProjected = Vector3.ProjectOnPlane(originalFrontLookVectorWorldSpace, hitNormalWorldSpace);

                Quaternion rotationAdjustmentForward = Quaternion.FromToRotation(carSpawnPoint.forward, lookDirectionProjected);
                carSpawnPoint.rotation = rotationAdjustmentForward;

                float angle = Vector3.Angle(carSpawnPoint.forward, lookDirectionProjected);
                carSpawnPoint.Rotate(-angle, 0f, 0f);

                angle = Vector3.Angle(carSpawnPoint.right, hit.normal);
                carSpawnPoint.Rotate(0f, 0f, -(90 - angle));

                angle = Vector3.Angle(carSpawnPoint.forward, hit.normal);
                carSpawnPoint.Rotate((90 - angle), 0f, 0f);

            }

            else
            {
                carSpawnPoints.Remove(carSpawnPoint); //if ray cast was not successful, that means point is not on road so remove it
            }


        }

        Time.fixedDeltaTime = 1f / fixedUpdateFrequency; //set physics frequency

        CreateClassesTextFile();



    }

    private void SpawnCars(Transform pickedCameraPoint)
    {
        foreach (GameObject spawnedCar in spawnedCars) //destroy cars spawned in the previous frame
        {
            DestroyImmediate(spawnedCar);
        }

        spawnedCars.Clear(); //clear spawned cars lists for new cars in this frame to spawn

        foreach (Transform carSpawnPoint in carSpawnPoints)
        {

            if (UnityEngine.Random.Range(0f, 1f) > carSpawnChance || (carSpawnPoint.position - pickedCameraPoint.position).magnitude > relevantRadiusToCamera)
            {
                continue;
            }

            GameObject randomlyPickedCar = cars[UnityEngine.Random.Range(0, cars.Count)];
            GameObject spawnedCar = Instantiate(randomlyPickedCar, carSpawnPoint);

            if (toggleRandomCarColors == true && UnityEngine.Random.Range(0f,1f) < randomCarColorChance)
            {
                MeshRenderer[] carMeshRenders = spawnedCar.GetComponentsInChildren<MeshRenderer>();

                foreach (MeshRenderer meshRenderer in carMeshRenders)
                {
                    foreach (Material material in meshRenderer.materials)
                    {
                        material.SetColor("_Color", UnityEngine.Random.ColorHSV());
                    }
                }
            }

            float randomXPoisition = UnityEngine.Random.Range(-randomPositionAmount, randomPositionAmount);
            float randomZPoisition = UnityEngine.Random.Range(-randomPositionAmount, randomPositionAmount);
            float randomRotation = UnityEngine.Random.Range(-randomRotationAmount, randomRotationAmount); //randomize poisition and rotation

            spawnedCar.transform.position += carSpawnPoint.TransformDirection(new Vector3(randomXPoisition, 0f, randomZPoisition));
            spawnedCar.transform.Rotate(0f, randomRotation, 0f); //apply randomness to position and rotation

            spawnedCars.Add(spawnedCar); //add spawned cars to list
        }
    }

    private void CreateClassesTextFile()
    {
        using (StreamWriter sw = File.CreateText(dataset.FullName + "/classes.txt"))
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

    private void CaptureImageSynthesis()
    {
        IS.Save(fixedUpdateCount.ToString(), captureWidth, captureHeight, dataset.FullName);
    }

    public void SaveTexture()
    {
        Texture2D myTex = toTexture2D(camRenderTexture);
        byte[] bytes = myTex.EncodeToJPG();
        System.IO.File.WriteAllBytes(dataset.FullName + "/" + fixedUpdateCount + ".png", bytes);
        Destroy(myTex); // important for preventing memory leak
    }

    private Texture2D toTexture2D(RenderTexture rTex)
    {
        Texture2D tex = new Texture2D(camRenderTexture.width, camRenderTexture.height, TextureFormat.RGB24, false);
        RenderTexture.active = rTex;
        tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
        tex.Apply();
        UnityEngine.Object.Destroy(tex);
        return tex;
    }

    private void FixedUpdate()
    {

        if (toggleRandomSunDirection == true)
        {

            float sunAngleDeviation = UnityEngine.Random.Range(-randomSunDeviation, randomSunDeviation);


            sun.rotation = Quaternion.Euler(90 + sunAngleDeviation, 0f, 0f);
        }
        Resources.UnloadUnusedAssets();

        Transform pickedCameraPoint = carSpawnPoints[UnityEngine.Random.Range(0, carSpawnPoints.Count)];

        SpawnCars(pickedCameraPoint);

        foreach (Transform child in pickedCameraPoint)
        {
            GameObject.Destroy(child.gameObject);
        }

        generationCamera.transform.position = pickedCameraPoint.position + pickedCameraPoint.TransformDirection(2f * Vector3.up);
        generationCamera.transform.rotation = pickedCameraPoint.rotation;


        float randomPosition = UnityEngine.Random.Range(-randomCameraPositionAmount, randomCameraPositionAmount);
        float randomRotation = UnityEngine.Random.Range(-randomCameraRotationAmount, randomCameraRotationAmount);
        float randomYRotation = UnityEngine.Random.Range(-randomCameraRotationAmountY, randomCameraRotationAmountY);

        generationCamera.transform.position += generationCamera.transform.TransformDirection(new Vector3(randomPosition, randomPosition, randomPosition));
        generationCamera.transform.Rotate(randomRotation, randomRotation, randomRotation);
        generationCamera.transform.Rotate(0f, randomYRotation, 0f);


        CaptureImageSynthesis(); //after spawning cars; take a picture, then process ground truth

        Collider[] hitColliders = Physics.OverlapSphere(generationCamera.transform.position, cameraBoundingBoxDistance, ~0, QueryTriggerInteraction.Collide);
        List<RootObject> rootObjects = new List<RootObject>(); //valid objects within view

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.gameObject.GetComponent<RootObject>() != null)
            {
                Vector3 screenPoint = generationCamera.WorldToViewportPoint(hitCollider.transform.position);
                bool onScreen = screenPoint.z > 0f && screenPoint.x > 0f && screenPoint.x < 1f && screenPoint.y > 0f && screenPoint.y < 1f;

                if (onScreen)
                {
                    rootObjects.Add(hitCollider.gameObject.GetComponent<RootObject>()); 
                }
            }
        }

        using (StreamWriter sw = File.CreateText(dataset.FullName + "/" + fixedUpdateCount + ".txt"))
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

                    if (Physics.Raycast(generationCamera.transform.position, corner - generationCamera.transform.position, out hit, Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
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

                for (int i = 0; i < pts3D.Length; i++)
                {
                    pts3D[i] = generationCamera.WorldToScreenPoint(pts3D[i]); //convert global space box corners to camera space
                }

                Vector3 min = pts3D[0];
                Vector3 max = pts3D[0];

                for (int i = 1; i < pts3D.Length; i++)
                {
                    min = Vector3.Min(min, pts3D[i]);
                    max = Vector3.Max(max, pts3D[i]);
                }

                min.y = Screen.height - min.y;
                max.y = Screen.height - max.y; //changing direction of y axis

                //Construct a rect of the min and max positions and apply some margin

                Rect r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

                float XCoord = (r.xMin + r.xMax) / 2;
                float YCoord = (r.yMin + r.yMax) / 2; // x and y center in screen space

                float height = Mathf.Abs(r.yMin - r.yMax);
                float width = Mathf.Abs(r.xMin - r.xMax); //height and width of box in screen space

                /*

                if (height < 12 || width < 12)
                {
                    continue;
                }

                */

                XCoord = XCoord / Screen.width;
                YCoord = YCoord / Screen.height;
                height = height / Screen.height;
                width = width / Screen.width; //normalize ground truth to screen dimensions for YOLOv3 labelling format

                if (classes.ContainsKey(className) && XCoord > labellingPad / Screen.width && XCoord < 1 - labellingPad / Screen.width && YCoord > labellingPad / Screen.height && YCoord < 1 - labellingPad / Screen.height)
                {
                    sw.WriteLine(String.Format("{0} {1} {2} {3} {4}", classes[className], XCoord, YCoord, width, height));
                } //checks if center of box is within specified padded area. prevents bad labels (negative centers)
            }
        }

        //SaveTexture();

        fixedUpdateCount++;

        if (fixedUpdateCount == captureNumber)
        {
            UnityEditor.EditorApplication.isPlaying = false;
        }
    }

    // Update is called once per frame
    private void Update()
    {
    }
}