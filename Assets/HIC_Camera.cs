using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Drawing;


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

    public int captureWidth = 1200;
    public int captureHeight = 800;

    private int fixedUpdateIterations;

    private int cameraCaptureCount = 0;

    Camera leftCamera;
    Camera rightCamera;

    public int captureFrames = 100;

    public float cameraBoundingBoxDistance = 10f;

    public bool toggleYOLOFormatRight = false;

    public List<GameObject> spawnCarPrefabs;

    private List<GameObject> previousSpawnedCars;

    public int maxNumberOfCars;

    public Projector mainProjector;

    int viewCount = 0;

    int viewIndex = 0;

    public List<List<Texture2D>> directionTexLists = new List<List<Texture2D>>();

    string streetviewImagesRootDir = @"C:\Repos\Streetview-Synthetic-Data-Generation\directory";

    public ReflectionProbe sceneReflectionProbe;

    int renderID;

    public float carSpawnYOffset;

    Dictionary<int, string> facingDirections = new Dictionary<int, string>()
    {
        { 0, "front"},
        { 1, "right"},
        { 2, "back"},
        { 3, "left"},
        { 4, "up"},
        { 5, "down"},
    };

    int delayAmount = 20;

    int frameDelay = 0;

    void Start()
    {


        previousSpawnedCars = new List<GameObject>();


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

        for (int i = 0; i < 6; i++)
        {

            List<Texture2D> texList = new List<Texture2D>();
            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(streetviewImagesRootDir, facingDirections[i]));
            FileInfo[] ImageFiles = tempDir.GetFiles("*.jpg"); //Getting Text files

            foreach (FileInfo file in ImageFiles)
            {

                Texture2D tex2D = LoadPNG(file.FullName);
                //tex2D.wrapMode = TextureWrapMode.Clamp;
                tex2D.name = file.FullName;
                texList.Add(tex2D);

            }

            directionTexLists.Add(texList);

        }


        viewCount = directionTexLists[0].Count; //6 images in one view

        renderID = sceneReflectionProbe.RenderProbe();


    }

    // Update is called once per frame
    void FixedUpdate()
    {

        if (sceneReflectionProbe.IsFinishedRendering(renderID) && frameDelay == delayAmount)
        {

            frameDelay = 0;

            viewIndex = UnityEngine.Random.Range(0, viewCount);

            mainProjector.material.SetTexture("_ShadowTex", directionTexLists[0][viewIndex]);

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


            previousSpawnedCars.Clear();

            for (int i = 0; i < UnityEngine.Random.Range(1, maxNumberOfCars); i++)
            {
                GameObject spawnedCar = Instantiate(spawnCarPrefabs[UnityEngine.Random.Range(0, spawnCarPrefabs.Count)]);
                spawnedCar.transform.position = transform.position + UnityEngine.Random.Range(1f, 40f) * transform.forward + UnityEngine.Random.Range(-7f, 7f) * transform.right + -carSpawnYOffset * transform.up;
                spawnedCar.transform.Rotate(0f, UnityEngine.Random.Range(-20f, 20f), 0f);
                previousSpawnedCars.Add(spawnedCar);
            }

            Shader skyboxMatShader = Shader.Find("Skybox/6 Sided");
            Material skyboxMatTemp = new Material(skyboxMatShader);
            skyboxMatTemp.SetTexture("_FrontTex", directionTexLists[0][viewIndex]); //get first tex list which is for the fronts, then get the index image
            skyboxMatTemp.SetTexture("_RightTex", directionTexLists[3][viewIndex]); //left and right texs need to be switched
            skyboxMatTemp.SetTexture("_BackTex", directionTexLists[2][viewIndex]);
            skyboxMatTemp.SetTexture("_LeftTex", directionTexLists[1][viewIndex]);
            skyboxMatTemp.SetTexture("_UpTex", directionTexLists[4][viewIndex]);
            skyboxMatTemp.SetTexture("_DownTex", directionTexLists[5][viewIndex]);

            RenderSettings.skybox = skyboxMatTemp;

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
                UnityEditor.EditorApplication.isPlaying = false;
            }
        }


        else
        {
            if (frameDelay == delayAmount - 10)
            {
                BoundingBoxUtils.SaveImageAndBoundingBoxes(cameraChassis, rightCamera, cameraBoundingBoxDistance, rightCamDir, fixedUpdateIterations, captureWidth, captureHeight, classes, toggleYOLOFormatRight, 0);
                cameraCaptureCount++;
            }
            frameDelay++;
        }
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
}
