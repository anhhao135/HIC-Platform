using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class HIC_Camera : MonoBehaviour
{
    //rgb 140 140 140 is road for homography seg

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

    public enum motions { Static, Linear, Oscillate };

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

    public int captureWidth = 1200;
    public int captureHeight = 800;

    private int fixedUpdateIterations;

    private int cameraCaptureCount = 0;

    private Camera leftCamera;
    private Camera rightCamera;

    public int captureFrames = 100;

    public float cameraBoundingBoxDistance = 10f;

    public bool toggleYOLOFormatRight = false;

    public List<GameObject> spawnCarPrefabs;

    private List<GameObject> previousSpawnedCars;

    public int maxNumberOfCars;

    public Projector mainProjector;

    private int viewCount = 0;

    private int viewIndex = 0;

    public List<List<Texture2D>> directionTexLists = new List<List<Texture2D>>();

    private string streetviewImagesRootDir = @"C:\Repos\integrated-synthetic-pipeline\directory";

    public ReflectionProbe sceneReflectionProbe;

    private int renderID;

    public float carSpawnYOffset;

    public bool toggleStereoCameraBaseline = false;

    public float bboxOcclusionRatio = 0.5f;

    public Light sun;

    private Dictionary<int, string> facingDirections = new Dictionary<int, string>()
    {
        { 0, "front_valid"},
        { 1, "right"},
        { 2, "back"},
        { 3, "left"},
        { 4, "up"},
        { 5, "down"},
        { 6, "homography"}
    };

    private int delayAmount = 5;

    private int frameDelay = 0;

    public Transform homographyPlane;

    private DirectoryInfo ImgDir;
    private DirectoryInfo AnnotationsDir;

    public float carCollisionRadius = 2.5f;

    private System.Random RNG = new System.Random();

    private Material previousSkybox;

    Vector3 cameraStartPosition;

    public bool toggleRandomCameraPlacement = false;

    private void Start()
    {
        previousSpawnedCars = new List<GameObject>();

        fixedUpdateIterations = 0;

        if (toggleCameraCapture == true)
        {
            leftCamDir = Directory.CreateDirectory(string.Format("Session_{0:yyyy-MM-dd}", DateTime.Now) + "_" + "left" + "_" + gameObject.name);
            rightCamDir = Directory.CreateDirectory(string.Format("Session_{0:yyyy-MM-dd}", DateTime.Now) + "_" + "right" + "_" + gameObject.name);
            ImgDir = Directory.CreateDirectory(Path.Combine(rightCamDir.ToString(), "Img"));
            AnnotationsDir = Directory.CreateDirectory(Path.Combine(rightCamDir.ToString(), "Annotations"));
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
                if (toggleStereoCameraBaseline == true)
                {
                    mono_cam.transform.localPosition = new Vector3(baseline / 2f, 0f, 0f);
                }

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
                if (toggleStereoCameraBaseline == true)
                {
                    mono_cam.transform.localPosition = new Vector3(-baseline / 2f, 0f, 0f);
                }

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

        cameraStartPosition = rightCamera.transform.position;

        DirectoryInfo frontValidDir = new DirectoryInfo(Path.Combine(streetviewImagesRootDir, facingDirections[0]));
        FileInfo[] frontValidImages = frontValidDir.GetFiles("*.jpg");

        List<string> frontValidNames = new List<string>();

        List<Texture2D> frontValidTexList = new List<Texture2D>();

        foreach (FileInfo file in frontValidImages)
        {
            string validName = Path.GetFileName(file.FullName);
            frontValidNames.Add(validName);
            Texture2D tex2D = LoadPNG(file.FullName);
            tex2D.name = file.FullName;
            frontValidTexList.Add(tex2D);
        }

        directionTexLists.Add(frontValidTexList);

        for (int i = 1; i < 7; i++)
        {
            List<Texture2D> texList = new List<Texture2D>();
            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(streetviewImagesRootDir, facingDirections[i]));

            foreach (string validFileName in frontValidNames)
            {
                string validImagePath;

                if (i == 6)
                {
                    validImagePath = Path.Combine(tempDir.FullName, Path.GetFileNameWithoutExtension(validFileName) + ".png");
                }
                else
                {
                    validImagePath = Path.Combine(tempDir.FullName, validFileName);
                }

                Texture2D tex2D = LoadPNG(validImagePath);
                tex2D.name = validImagePath;
                texList.Add(tex2D);
            }

            directionTexLists.Add(texList);
        }

        viewCount = directionTexLists[0].Count; //6 images in one view

        renderID = sceneReflectionProbe.RenderProbe();
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        rightCamera.GetComponent<ImageSynthesis>().OnSceneChange();

        if (sceneReflectionProbe.IsFinishedRendering(renderID) && frameDelay == delayAmount)
        {
            frameDelay = 0;

            viewIndex = UnityEngine.Random.Range(0, viewCount);

            //mainProjector.material.SetTexture("_ShadowTex", directionTexLists[0][viewIndex]);

            switch (movementMode)
            {
                case motions.Static: //stat
                    break;

                case motions.Linear: //lin
                    float period = ((linearRange / 2f) / linearSpeed) * 4f;
                    cameraChassis.localPosition = new Vector3(TriangleWave(timeSinceStart, linearRange / 2f, period), 0f, 0f);
                    break;

                case motions.Oscillate: //osc
                    float timeParameter = (3 * Mathf.PI) / 2 + (TriangleWave(timeSinceStart * oscillationAngularSpeed * Mathf.Deg2Rad, Mathf.PI / 2f, (Mathf.PI / 2f) * 4f)); //goes between pi and 2pi
                    Vector3 localPositionSemiCircle = new Vector3(oscillationRadius * Mathf.Cos(timeParameter), oscillationRadius * Mathf.Sin(timeParameter), 0f);
                    cameraChassis.localPosition = localPositionSemiCircle;
                    break;
            }

            if (previousSpawnedCars.Any())
            {
                foreach (GameObject previousSpawnedCar in previousSpawnedCars)
                {
                    DestroyImmediate(previousSpawnedCar);
                }
            }

            //spawn cars

            previousSpawnedCars.Clear();

            Color[] homographyPixels = directionTexLists[6][viewIndex].GetPixels();

            //homography image is 1000 x 1000

            int randomNum = RNG.Next(1, maxNumberOfCars);

            for (int i = 0; i < randomNum; ++i)
            {
                Color32 sampledSegColor = new Color32();

                bool collision = true;

                int x = 0;
                int y = 0;

                Vector3 spawnPosition = Vector3.zero;

                int placementTries = 0;

                bool validPosition = true;

                while (sampledSegColor.r != 140 || sampledSegColor.g != 140 || sampledSegColor.b != 140 || collision)
                {
                    x = UnityEngine.Random.Range(0, 999);
                    y = UnityEngine.Random.Range(0, 350);

                    int flatIndex = y * 1000 + x;

                    sampledSegColor = homographyPixels[flatIndex];

                    spawnPosition = homographyPlane.transform.position + new Vector3(-50f, 0f, -50f) + new Vector3(x / 10f, 0f, y / 10f);

                    collision = false;

                    if (previousSpawnedCars.Count > 0)
                    {
                        foreach (GameObject previousSpawnedCar in previousSpawnedCars)
                        {
                            if (Vector3.Magnitude(previousSpawnedCar.transform.position - spawnPosition) < 2 * carCollisionRadius)
                            {
                                collision = true;
                            }
                        }
                    }

                    placementTries++;

                    if (placementTries > 100) //max number of possible positions
                    {
                        validPosition = false;
                        break;
                    }
                }

                if (validPosition == true)
                {
                    GameObject spawnedCar = Instantiate(spawnCarPrefabs[UnityEngine.Random.Range(0, spawnCarPrefabs.Count)]);

                    spawnedCar.transform.position = spawnPosition;

                    //Vector3 localSpawnPosition = UnityEngine.Random.Range(-50f, 50f) * transform.forward + UnityEngine.Random.Range(-50f, 50f) * transform.right; //scaled down by 10 because plane is e

                    //spawnedCar.transform.position = transform.position +

                    if (UnityEngine.Random.Range(0f, 1f) < 0.5f)
                    {
                        spawnedCar.transform.Rotate(0f, UnityEngine.Random.Range(-5f, 5f), 0f);
                    }

                    else
                    {
                        spawnedCar.transform.Rotate(0f, 180f + UnityEngine.Random.Range(-5f, 5f), 0f);
                    }

                    

                    CarMaterialChange spawnedCarChanger = spawnedCar.transform.GetComponent<CarMaterialChange>();

                    Renderer[] renderers = spawnedCar.GetComponentsInChildren<Renderer>();


                    

                    


                    float randomGreyfloat = UnityEngine.Random.Range(0.05f, 0.95f);

                    Color randomGreyscale = new Color(randomGreyfloat, randomGreyfloat, randomGreyfloat);

                    float randomGloss = UnityEngine.Random.Range(0.75f, 0.99f);

                    foreach (Renderer renderer in renderers)
                    {
                        foreach (Material carMaterial in renderer.materials)
                        {
                            if ((carMaterial.name == spawnedCarChanger.materialName) || (carMaterial.name == spawnedCarChanger.materialName + " (Instance)"))
                            {

                                carMaterial.SetColor("_Color", randomGreyscale);
                                carMaterial.SetFloat("_Glossiness", randomGloss);
                            }
                        }
                    }

                    

                    previousSpawnedCars.Add(spawnedCar);

                    
                }
            }

            //end spawn cars

            if (previousSkybox != null)
            {
                DestroyImmediate(previousSkybox);
            }

            Shader skyboxMatShader = Shader.Find("Skybox/6 Sided");
            Material skyboxMatTemp = new Material(skyboxMatShader);
            skyboxMatTemp.SetTexture("_FrontTex", directionTexLists[0][viewIndex]); //get first tex list which is for the fronts, then get the index image
            skyboxMatTemp.SetTexture("_RightTex", directionTexLists[3][viewIndex]); //left and right texs need to be switched
            skyboxMatTemp.SetTexture("_BackTex", directionTexLists[2][viewIndex]);
            skyboxMatTemp.SetTexture("_LeftTex", directionTexLists[1][viewIndex]);
            skyboxMatTemp.SetTexture("_UpTex", directionTexLists[4][viewIndex]);
            skyboxMatTemp.SetTexture("_DownTex", directionTexLists[5][viewIndex]);

            previousSkybox = skyboxMatTemp;

            RenderSettings.skybox = skyboxMatTemp;

            //homographyPlane.GetComponent<Renderer>().material.SetTexture("_MainTex", directionTexLists[6][viewIndex]);

            renderID = sceneReflectionProbe.RenderProbe();

            /*

            Cubemap reflectionProbeCupeMap = new Cubemap(640, TextureFormat.RGBA32, false);
            reflectionProbeCupeMap.SetPixels(directionTexLists[0][viewIndex].GetPixels(), CubemapFace.PositiveZ);
            reflectionProbeCupeMap.SetPixels(directionTexLists[3][viewIndex].GetPixels(), CubemapFace.PositiveX);
            reflectionProbeCupeMap.SetPixels(directionTexLists[2][viewIndex].GetPixels(), CubemapFace.NegativeZ);
            reflectionProbeCupeMap.SetPixels(directionTexLists[1][viewIndex].GetPixels(), CubemapFace.NegativeX);
            reflectionProbeCupeMap.SetPixels(directionTexLists[4][viewIndex].GetPixels(), CubemapFace.PositiveY);
            reflectionProbeCupeMap.SetPixels(directionTexLists[5][viewIndex].GetPixels(), CubemapFace.NegativeY);

            sceneReflectionProbe.customBakedTexture = reflectionProbeCupeMap;

            */

            /*
            { 0, "front"},
            { 1, "right"},
            { 2, "back"},
            { 3, "left"},
            { 4, "up"},
            { 5, "down"},
            */

            viewIndex++;

            //BoundingBoxUtils.SaveImageAndBoundingBoxes(cameraChassis, leftCamera, cameraBoundingBoxDistance, leftCamDir, fixedUpdateIterations, captureWidth, captureHeight, classes, toggleYOLOFormatRight, 0);

            timeSinceStart = timeSinceStart + framePeriod;

            fixedUpdateIterations++;

            if (toggleCameraCapture == true && cameraCaptureCount == captureFrames)
            {
                string[] ImgFileNames = Directory.GetFiles(AnnotationsDir.FullName);

                System.Random random = new System.Random();
                ImgFileNames = ImgFileNames.OrderBy(x => random.Next()).ToArray();
                int fileCount = ImgFileNames.Length;
                int splitIndex = (int)(0.95f * fileCount);

                String[] TrainValNames = SubArray(ImgFileNames, 0, splitIndex - 1);
                String[] TestNames = SubArray(ImgFileNames, splitIndex, fileCount - splitIndex);

                using (StreamWriter writer = new StreamWriter(Path.Combine(rightCamDir.FullName, "trainval.txt")))
                {
                    foreach (string fullPath in TrainValNames)
                    {
                        if (new FileInfo(fullPath).Length > 0)
                        {
                            writer.WriteLine(Path.GetFileNameWithoutExtension(fullPath));
                        }
                            
                    }
                }

                using (StreamWriter writer = new StreamWriter(Path.Combine(rightCamDir.FullName, "test.txt")))
                {
                    foreach (string fullPath in TestNames)
                    {
                        if (new FileInfo(fullPath).Length > 0)
                        {
                            writer.WriteLine(Path.GetFileNameWithoutExtension(fullPath));
                        }

                    }
                }

                UnityEditor.EditorApplication.isPlaying = false;
            }
        }
        else
        {
            if (frameDelay == (int)(0.5f * delayAmount) && fixedUpdateIterations != 0)
            {

                if (previousSpawnedCars.Count > 0)
                {

                    

                    RandomizeCameraPlacement(rightCamera, 15f, 1f, 0.3f);

                    sun.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f));

                    BoundingBoxUtils.SaveImageAndBoundingBoxes(cameraChassis, rightCamera, cameraBoundingBoxDistance, rightCamDir, fixedUpdateIterations, captureWidth, captureHeight, classes, toggleYOLOFormatRight, bboxOcclusionRatio, 0);
                }
                


                cameraCaptureCount++;
            }
            frameDelay++;
        }

        Resources.UnloadUnusedAssets();
    }

    public void RandomizeCameraPlacement(Camera camera, float range, float rangeY, float rangeZ)
    {
        camera.transform.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-range/4f, range/4f), UnityEngine.Random.Range(-range, range), UnityEngine.Random.Range(-range/4f, range/4f));

        camera.transform.position = cameraStartPosition + new Vector3(0, UnityEngine.Random.Range(-rangeY, 0), UnityEngine.Random.Range(-rangeZ, rangeZ));
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

    public static Texture2D LoadPNG(string filePath)
    {
        Texture2D tex = null;
        byte[] fileData;

        if (File.Exists(filePath))
        {
            fileData = File.ReadAllBytes(filePath);
            tex = new Texture2D(2, 2);
            tex.LoadImage(fileData); //..this will auto-resize the texture dimensions.
        }
        return tex;
    }

    private T[] SubArray<T>(T[] data, int index, int length)
    {
        T[] result = new T[length];
        Array.Copy(data, index, result, 0, length);
        return result;
    }
}