﻿using System;
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

    Camera leftCamera;
    Camera rightCamera;

    public int captureFrames = 100;

    public float cameraBoundingBoxDistance = 10f;

    public bool toggleYOLOFormatRight = false;

    public List<GameObject> spawnCarPrefabs;

    private List<GameObject> previousSpawnedCars;

    public int maxNumberOfCars;

    public Projector mainProjector;

    public DirectoryInfo streetviewImages;

    public List<Texture2D> texList;

    void Start()
    {

        streetviewImages = new DirectoryInfo(@"C:\Users\h3le\Desktop\Streetview-Synthetic-Data-Generation\directory");

        FileInfo[] Files = streetviewImages.GetFiles("*.jpg"); //Getting Text files


        foreach (FileInfo file in Files)
        {

            Texture2D tex2D = LoadPNG(file.FullName);
            texList.Add(tex2D);

        }


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
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        mainProjector.material.SetTexture("_ShadowTex", texList[UnityEngine.Random.Range(0, texList.Count)]);
        
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
            spawnedCar.transform.position = transform.position + UnityEngine.Random.Range(1f, 40f) * transform.forward + UnityEngine.Random.Range(-7f, 7f) * transform.right + -1.8f * transform.up;
            spawnedCar.transform.Rotate(0f, UnityEngine.Random.Range(-20f, 20f), 0f);
            previousSpawnedCars.Add(spawnedCar);
        }




        //BoundingBoxUtils.SaveImageAndBoundingBoxes(cameraChassis, leftCamera, cameraBoundingBoxDistance, leftCamDir, fixedUpdateIterations, captureWidth, captureHeight, classes, toggleYOLOFormatRight, 0);

        BoundingBoxUtils.SaveImageAndBoundingBoxes(cameraChassis, rightCamera, cameraBoundingBoxDistance, rightCamDir, fixedUpdateIterations, captureWidth, captureHeight, classes, toggleYOLOFormatRight, 0);


        timeSinceStart = timeSinceStart + framePeriod;

        fixedUpdateIterations++;


        if (toggleCameraCapture == true && fixedUpdateIterations == captureFrames)
        {
            UnityEditor.EditorApplication.isPlaying = false;
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
