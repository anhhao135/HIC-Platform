using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// @TODO:
// . support custom color wheels in optical flow via lookup textures
// . support custom depth encoding
// . support multiple overlay cameras
// . tests
// . better example scene(s)

// @KNOWN ISSUES
// . Motion Vectors can produce incorrect results in Unity 5.5.f3 when
//      1) during the first rendering frame
//      2) rendering several cameras with different aspect ratios - vectors do stretch to the sides of the screen

[RequireComponent(typeof(Camera))]
public class ImageSynthesis : MonoBehaviour
{
    public bool multichannelDepth = false;
    public Dictionary<string, string> instanceSegDict = new Dictionary<string, string>();
    public Dictionary<int, Color> instanceSegDictColor = new Dictionary<int, Color>();

    public float depthFarClipPlane = 20f;

    public bool toggleDepth = false;

    public bool toggleSeg = false;

    // pass configuration

    private CapturePass[] capturePasses = new CapturePass[] {
        new CapturePass() { name = "_img" },
        new CapturePass() { name = "_id", supportsAntialiasing = false },
        new CapturePass() { name = "_layer", supportsAntialiasing = false },
        new CapturePass() { name = "_depth" },
        new CapturePass() { name = "_normals" },
        new CapturePass() { name = "_flow", supportsAntialiasing = false, needsRescale = true }, // (see issue with Motion Vectors in @KNOWN ISSUES)
		new CapturePass() { name = "_tag", supportsAntialiasing = false },
    };

    private struct CapturePass
    {
        // configuration
        public string name;

        public bool supportsAntialiasing;
        public bool needsRescale;

        public CapturePass(string name_)
        {
            name = name_; supportsAntialiasing = true; needsRescale = false; camera = null;
        }

        // impl
        public Camera camera;
    };

    public Shader uberReplacementShader;
    public Shader opticalFlowShader;

    public float opticalFlowSensitivity = 1.0f;

    // cached materials
    private Material opticalFlowMaterial;

    private void Start()
    {
        // default fallbacks, if shaders are unspecified
        if (!uberReplacementShader)
            uberReplacementShader = Shader.Find("Hidden/UberReplacement");

        if (!opticalFlowShader)
            opticalFlowShader = Shader.Find("Hidden/OpticalFlow");

        // use real camera to capture final image
        capturePasses[0].camera = GetComponent<Camera>();
        for (int q = 1; q < capturePasses.Length; q++)
            capturePasses[q].camera = CreateHiddenCamera(capturePasses[q].name);

        OnCameraChange();
        OnSceneChange();
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        if (DetectPotentialSceneChangeInEditor())
            OnSceneChange();
#endif // UNITY_EDITOR

        // @TODO: detect if camera properties actually changed
        OnCameraChange();
    }

    public void SaveDictionaryAsText(string filePath)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate))
        {
            using (TextWriter tw = new StreamWriter(fs))

                foreach (KeyValuePair<string, string> kvp in instanceSegDict)
                {
                    tw.WriteLine(string.Format("{0};{1}", kvp.Key, kvp.Value));
                }
        }
    }

    private Camera CreateHiddenCamera(string name)
    {
        var go = new GameObject(name, typeof(Camera));
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.parent = transform;

        var newCamera = go.GetComponent<Camera>();
        return newCamera;
    }

    static private void SetupCameraWithReplacementShader(Camera cam, Shader shader, ReplacelementModes mode)
    {
        SetupCameraWithReplacementShader(cam, shader, mode, Color.black);
    }

    static private void SetupCameraWithReplacementShader(Camera cam, Shader shader, ReplacelementModes mode, Color clearColor)
    {
        var cb = new CommandBuffer();
        cb.SetGlobalFloat("_OutputMode", (int)mode); // @TODO: CommandBuffer is missing SetGlobalInt() method

        cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cb);
        cam.AddCommandBuffer(CameraEvent.BeforeFinalPass, cb);
        cam.SetReplacementShader(shader, "RenderType");
        cam.backgroundColor = clearColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    static private void SetupCameraWithPostShader(Camera cam, Material material, DepthTextureMode depthTextureMode = DepthTextureMode.None)
    {
        var cb = new CommandBuffer();
        cb.Blit(null, BuiltinRenderTextureType.CurrentActive, material);
        cam.AddCommandBuffer(CameraEvent.AfterEverything, cb);
        cam.depthTextureMode = depthTextureMode;
    }

    private enum ReplacelementModes
    {
        ObjectId = 0,
        CatergoryId = 1,
        DepthCompressed = 2,
        DepthMultichannel = 3,
        Normals = 4,
        TagId = 5,
    };

    public void OnCameraChange()
    {
        int targetDisplay = 1;
        var mainCamera = GetComponent<Camera>();
        foreach (var pass in capturePasses)
        {
            if (pass.camera == mainCamera)
                continue;

            // cleanup capturing camera
            pass.camera.RemoveAllCommandBuffers();

            // copy all "main" camera parameters into capturing camera
            pass.camera.CopyFrom(mainCamera);

            // set targetDisplay here since it gets overriden by CopyFrom()
            pass.camera.targetDisplay = targetDisplay++;
        }

        // cache materials and setup material properties
        if (!opticalFlowMaterial || opticalFlowMaterial.shader != opticalFlowShader)
            opticalFlowMaterial = new Material(opticalFlowShader);
        opticalFlowMaterial.SetFloat("_Sensitivity", opticalFlowSensitivity);

        // setup command buffers and replacement shaders
        SetupCameraWithReplacementShader(capturePasses[1].camera, uberReplacementShader, ReplacelementModes.ObjectId);
        SetupCameraWithReplacementShader(capturePasses[2].camera, uberReplacementShader, ReplacelementModes.CatergoryId);

        if (multichannelDepth == true)
        {
            SetupCameraWithReplacementShader(capturePasses[3].camera, uberReplacementShader, ReplacelementModes.DepthMultichannel, Color.white);
        }
        else
        {
            SetupCameraWithReplacementShader(capturePasses[3].camera, uberReplacementShader, ReplacelementModes.DepthCompressed, Color.white);
            capturePasses[3].camera.farClipPlane = depthFarClipPlane;
        }

        //SetupCameraWithReplacementShader(capturePasses[4].camera, uberReplacementShader, ReplacelementModes.Normals);
        //SetupCameraWithPostShader(capturePasses[5].camera, opticalFlowMaterial, DepthTextureMode.Depth | DepthTextureMode.MotionVectors);
        //SetupCameraWithReplacementShader(capturePasses[6].camera, uberReplacementShader, ReplacelementModes.TagId);
    }

    public void OnSceneChange()
    {
        var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();

        var mpb = new MaterialPropertyBlock();

        foreach (var r in renderers)
        {
            int id;

            /*
			if (r.gameObject.transform.parent != null)
			{
				id = r.gameObject.transform.parent.gameObject.GetInstanceID();
			}
			else
			{
				id = r.gameObject.GetInstanceID();
			}
			*/

            //id = r.gameObject.GetInstanceID();

            GameObject rootObject = r.gameObject.transform.root.gameObject;

            if (rootObject != null)
            {
                id = rootObject.GetInstanceID();

                string parentObjectTag;
                Color encodedIDColor;

                try
                {
                    parentObjectTag = rootObject.tag;
                }
                catch
                {
                    continue;
                }

                if (parentObjectTag != "car")
                {
                    continue;
                }

                if (r is SpriteRenderer)
                {
                    r.material.SetColor("_ObjectColor", encodedIDColor = ColorEncoding.EncodeIDAsColor(id));

                    //mpb.SetColor("_ObjectColor", encodedIDColor = ColorEncoding.EncodeIDAsColor(id));
                    //mpb.SetColor("_CategoryColor", ColorEncoding.EncodeLayerAsColor(layer));
                    //mpb.SetColor("_TagColor", ColorEncoding.EncodeTagAsColor(tag));

                    //r.SetPropertyBlock(mpb);

                    //continue;
                }
                else
                {
                    mpb.SetColor("_ObjectColor", encodedIDColor = ColorEncoding.EncodeIDAsColor(id));
                    //mpb.SetColor("_CategoryColor", ColorEncoding.EncodeLayerAsColor(layer));
                    //mpb.SetColor("_TagColor", ColorEncoding.EncodeTagAsColor(tag));

                    r.SetPropertyBlock(mpb);
                }

                //var layer = r.gameObject.layer;
                //var tag = r.gameObject.tag;
                //collider.transform.root

                try
                {
                    instanceSegDict.Add(((Color32)encodedIDColor).ToString(), id.ToString());
                    instanceSegDictColor.Add(id, encodedIDColor);
                }
                catch
                {
                    continue;
                }
            }
        }
    }

    public void Save(string filename, int width = -1, int height = -1, string path = "")
    {
        if (width <= 0 || height <= 0)
        {
            width = Screen.width;
            height = Screen.height;
        }

        var filenameExtension = System.IO.Path.GetExtension(filename);
        if (filenameExtension == "")
            filenameExtension = ".png";
        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

        var pathWithoutExtension = Path.Combine(path, filenameWithoutExtension);

        // execute as coroutine to wait for the EndOfFrame before starting capture
        StartCoroutine(
            WaitForEndOfFrameAndSave(pathWithoutExtension, filenameExtension, width, height, path));
    }

    private IEnumerator WaitForEndOfFrameAndSave(string filenameWithoutExtension, string filenameExtension, int width, int height, string path)
    {
        yield return new WaitForEndOfFrame();
        Save(filenameWithoutExtension, filenameExtension, width, height, path);
    }

    private void Save(string filenameWithoutExtension, string filenameExtension, int width, int height, string path)
    {
        /*
		foreach (var pass in capturePasses)
			Save(pass.camera, filenameWithoutExtension + pass.name + filenameExtension, width, height, pass.supportsAntialiasing, pass.needsRescale);

		*/
        Save(capturePasses[0].camera, filenameWithoutExtension + filenameExtension, width, height, capturePasses[0].supportsAntialiasing, capturePasses[0].needsRescale);
        //Save(capturePasses[0].camera, filenameWithoutExtension + capturePasses[0].name + filenameExtension, width, height, capturePasses[0].supportsAntialiasing, capturePasses[0].needsRescale); commented out bc saving img rgb without _img in name to match kitti.
        //save rgb
        //Save(capturePasses[1].camera, filenameWithoutExtension + capturePasses[1].name + filenameExtension, width, height, capturePasses[1].supportsAntialiasing, capturePasses[1].needsRescale);

        if (toggleDepth == true)
        {
            Save(capturePasses[3].camera, filenameWithoutExtension + capturePasses[3].name + filenameExtension, width, height, capturePasses[3].supportsAntialiasing, capturePasses[3].needsRescale); //save depth
        }

        if (toggleSeg == true)
        {
            Save(capturePasses[1].camera, filenameWithoutExtension + "_seg" + filenameExtension, width, height, capturePasses[1].supportsAntialiasing, capturePasses[1].needsRescale);
        }

        /*
        SavePointCloud(capturePasses[3].camera, filenameWithoutExtension + capturePasses[3].name + filenameExtension, width, height, capturePasses[3].supportsAntialiasing, capturePasses[3].needsRescale, path);

        */
    }

    private void SavePointCloud(Camera cam, string filename, int width, int height, bool supportsAntialiasing, bool needsRescale, string path)
    {
        var mainCamera = GetComponent<Camera>();
        var depth = 24;
        var format = RenderTextureFormat.Default;
        var readWrite = RenderTextureReadWrite.Default;
        var antiAliasing = (supportsAntialiasing) ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;

        var finalRT =
            RenderTexture.GetTemporary(width, height, depth, format, readWrite, antiAliasing);
        var renderRT = (!needsRescale) ? finalRT :
            RenderTexture.GetTemporary(mainCamera.pixelWidth, mainCamera.pixelHeight, depth, format, readWrite, antiAliasing);
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        var prevActiveRT = RenderTexture.active;
        var prevCameraRT = cam.targetTexture;

        // render to offscreen texture (readonly from CPU side)
        RenderTexture.active = renderRT;
        cam.targetTexture = renderRT;

        cam.Render();

        if (needsRescale)
        {
            // blit to rescale (see issue with Motion Vectors in @KNOWN ISSUES)
            RenderTexture.active = finalRT;
            Graphics.Blit(renderRT, finalRT);
            RenderTexture.ReleaseTemporary(renderRT);
        }

        // read offsreen texture contents into the CPU readable texture
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();

        float camAspect = cam.aspect;
        float verticalFOV = cam.fieldOfView;
        float horizontalFOV = Camera.VerticalToHorizontalFieldOfView(verticalFOV, camAspect);

        Color[] pix = tex.GetPixels();

        int totalPixels = pix.Length;

        //color is RGBA, 0001 is black, 1111 is white

        Vector3[] points = new Vector3[totalPixels];
        int[] indecies = new int[totalPixels];
        Color[] colors = new Color[totalPixels];

        for (int i = 0; i < totalPixels; ++i)
        {
            Color currentPixel = pix[i];

            if (currentPixel.r != 1)
            {
                int currentColumn;

                if (i < tex.width)
                {
                    currentColumn = i + 1;
                }
                else
                {
                    currentColumn = (i % tex.width) + 1;
                }

                float depthZ = currentPixel.r * cam.farClipPlane;

                float alphaHorizontal = (180 - horizontalFOV) / 2;
                float gammaHorizontal = alphaHorizontal + (currentColumn * horizontalFOV) / (tex.width);
                float deltaX = (depthZ) / (Mathf.Tan(gammaHorizontal * Mathf.Deg2Rad));

                float currentRow = Mathf.Ceil((i + 1) / tex.width);

                float gammaVertical = (180 - verticalFOV) / 2 + (verticalFOV / tex.height) * currentRow;
                float deltaY = (depthZ) / (Mathf.Tan(gammaVertical * Mathf.Deg2Rad));

                points[i] = new Vector3(-deltaX, -deltaY, depthZ);
                indecies[i] = i;
                //colors[i] = new Color(1 - currentPixel.r, currentPixel.r, 0, 1);
                colors[i] = Color.white;
            }
        }

        List<float[]> listOfVectors = new List<float[]>();

        int iteration_ = 0;

        foreach (Vector3 singleVector in points)
        {
            if (iteration_ % 37 == 0)
            {
                float[] newVector = new float[4];
                newVector[0] = singleVector.x;
                newVector[1] = singleVector.y;
                newVector[2] = singleVector.z;
                listOfVectors.Add(newVector);
            }

            iteration_++;
        }

        using (StreamWriter file = new StreamWriter(Path.Combine(path, Time.frameCount + "points.csv")))
        {
            foreach (float[] singleVector in listOfVectors)
            {
                file.WriteLine(String.Format("{0},{1},{2}", singleVector[0], singleVector[1], singleVector[2]));
            }
        }

        // restore state and cleanup
        cam.targetTexture = prevCameraRT;
        RenderTexture.active = prevActiveRT;

        UnityEngine.Object.Destroy(tex);
        RenderTexture.ReleaseTemporary(finalRT);
    }

    private void Save(Camera cam, string filename, int width, int height, bool supportsAntialiasing, bool needsRescale)
    {
        var mainCamera = GetComponent<Camera>();
        var depth = 24;
        var format = RenderTextureFormat.Default;
        var readWrite = RenderTextureReadWrite.Default;
        var antiAliasing = (supportsAntialiasing) ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;

        var finalRT =
            RenderTexture.GetTemporary(width, height, depth, format, readWrite, antiAliasing);
        var renderRT = (!needsRescale) ? finalRT :
            RenderTexture.GetTemporary(mainCamera.pixelWidth, mainCamera.pixelHeight, depth, format, readWrite, antiAliasing);
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        var prevActiveRT = RenderTexture.active;
        var prevCameraRT = cam.targetTexture;

        // render to offscreen texture (readonly from CPU side)
        RenderTexture.active = renderRT;
        cam.targetTexture = renderRT;

        cam.Render();

        if (needsRescale)
        {
            // blit to rescale (see issue with Motion Vectors in @KNOWN ISSUES)
            RenderTexture.active = finalRT;
            Graphics.Blit(renderRT, finalRT);
            RenderTexture.ReleaseTemporary(renderRT);
        }

        // read offsreen texture contents into the CPU readable texture
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();

        // encode texture into PNG
        var bytes = tex.EncodeToPNG();
        File.WriteAllBytes(filename, bytes);

        // restore state and cleanup
        cam.targetTexture = prevCameraRT;
        RenderTexture.active = prevActiveRT;

        DestroyImmediate(tex);
        RenderTexture.ReleaseTemporary(finalRT);
        //DestroyImmediate(finalRT);
        //DestroyImmediate(prevActiveRT);
        //DestroyImmediate(prevCameraRT);
        //DestroyImmediate(renderRT);
    }

    public Color[] GetSegmentationPixels()
    {
        Camera cam = capturePasses[1].camera;

        int width = Screen.width;
        int height = Screen.height;
        bool supportsAntialiasing = false;
        bool needsRescale = false;

        var mainCamera = GetComponent<Camera>();
        var depth = 24;
        var format = RenderTextureFormat.Default;
        var readWrite = RenderTextureReadWrite.Default;
        var antiAliasing = (supportsAntialiasing) ? Mathf.Max(1, QualitySettings.antiAliasing) : 1;

        var finalRT =
            RenderTexture.GetTemporary(width, height, depth, format, readWrite, antiAliasing);
        var renderRT = (!needsRescale) ? finalRT :
            RenderTexture.GetTemporary(mainCamera.pixelWidth, mainCamera.pixelHeight, depth, format, readWrite, antiAliasing);
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        var prevActiveRT = RenderTexture.active;
        var prevCameraRT = cam.targetTexture;

        // render to offscreen texture (readonly from CPU side)
        RenderTexture.active = renderRT;
        cam.targetTexture = renderRT;

        cam.Render();

        if (needsRescale)
        {
            // blit to rescale (see issue with Motion Vectors in @KNOWN ISSUES)
            RenderTexture.active = finalRT;
            Graphics.Blit(renderRT, finalRT);
            RenderTexture.ReleaseTemporary(renderRT);
        }

        // read offsreen texture contents into the CPU readable texture
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
        tex.Apply();

        // restore state and cleanup
        cam.targetTexture = prevCameraRT;
        RenderTexture.active = prevActiveRT;
        RenderTexture.ReleaseTemporary(finalRT);

        return tex.GetPixels();
        //DestroyImmediate(finalRT);
        //DestroyImmediate(prevActiveRT);
        //DestroyImmediate(prevCameraRT);
        //DestroyImmediate(renderRT);
    }

#if UNITY_EDITOR
    private GameObject lastSelectedGO;
    private int lastSelectedGOLayer = -1;
    private string lastSelectedGOTag = "unknown";

    private bool DetectPotentialSceneChangeInEditor()
    {
        bool change = false;
        // there is no callback in Unity Editor to automatically detect changes in scene objects
        // as a workaround lets track selected objects and check, if properties that are
        // interesting for us (layer or tag) did not change since the last frame
        if (UnityEditor.Selection.transforms.Length > 1)
        {
            // multiple objects are selected, all bets are off!
            // we have to assume these objects are being edited
            change = true;
            lastSelectedGO = null;
        }
        else if (UnityEditor.Selection.activeGameObject)
        {
            var go = UnityEditor.Selection.activeGameObject;
            // check if layer or tag of a selected object have changed since the last frame
            var potentialChangeHappened = lastSelectedGOLayer != go.layer || lastSelectedGOTag != go.tag;
            if (go == lastSelectedGO && potentialChangeHappened)
                change = true;

            lastSelectedGO = go;
            lastSelectedGOLayer = go.layer;
            lastSelectedGOTag = go.tag;
        }

        return change;
    }

#endif // UNITY_EDITOR
}