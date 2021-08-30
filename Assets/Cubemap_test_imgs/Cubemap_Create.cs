using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Drawing;

public class Cubemap_Create : MonoBehaviour
{
    // Start is called before the first frame update


    int viewCount = 0;

    int viewIndex = 0;

    public List<List<Texture2D>> directionTexLists = new List<List<Texture2D>>();

    string streetviewImagesRootDir = @"C:\Repos\Streetview-Synthetic-Data-Generation\directory";

    Dictionary<int, string> facingDirections = new Dictionary<int, string>()
    {
        { 0, "front"},
        { 1, "right"},
        { 2, "back"},
        { 3, "left"},
        { 4, "up"},
        { 5, "down"},
    };

    void Start()
    {

        for (int i = 0; i < 6; i++)
        {

            List<Texture2D> texList = new List<Texture2D>();
            DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(streetviewImagesRootDir, facingDirections[i]));
            FileInfo[] Files = tempDir.GetFiles("*.jpg"); //Getting Text files

            foreach (FileInfo file in Files)
            {

                Texture2D tex2D = LoadPNG(file.FullName);
                tex2D.wrapMode = TextureWrapMode.Clamp;
                tex2D.name = file.FullName;
                texList.Add(tex2D);

            }

            directionTexLists.Add(texList);

        }


        viewCount = directionTexLists[0].Count; //6 images in one view

    }

    // Update is called once per frame
    void Update()
    {
        
        Shader skyboxMatShader = Shader.Find("Skybox/6 Sided");
        Material skyboxMatTemp = new Material(skyboxMatShader);
        skyboxMatTemp.SetTexture("_FrontTex", directionTexLists[0][viewIndex]); //get first tex list which is for the fronts, then get the index image
        skyboxMatTemp.SetTexture("_RightTex", directionTexLists[3][viewIndex]); //left and right texs need to be switched
        skyboxMatTemp.SetTexture("_BackTex", directionTexLists[2][viewIndex]);
        skyboxMatTemp.SetTexture("_LeftTex", directionTexLists[1][viewIndex]);
        skyboxMatTemp.SetTexture("_UpTex", directionTexLists[4][viewIndex]);
        skyboxMatTemp.SetTexture("_DownTex", directionTexLists[5][viewIndex]);

        RenderSettings.skybox = skyboxMatTemp;


        /*
        { 0, "front"},
        { 1, "right"},
        { 2, "back"},
        { 3, "left"},
        { 4, "up"},
        { 5, "down"},
        */

        viewIndex++;



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
