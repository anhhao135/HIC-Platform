using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class SegTest : MonoBehaviour
{

    public ImageSynthesis IS; 
    private DirectoryInfo cameraDir;
    int i = 0;
    // Start is called before the first frame update
    void Start()
    {


        cameraDir = Directory.CreateDirectory("camera");


        

        
    }

    // Update is called once per frame
    void Update()
    {
        i++;

        IS.OnSceneChange();

        Color[] colors = IS.GetSegmentationPixels();

        if (i > 10)
        {
            IS.SaveDictionaryAsText(Path.Combine(cameraDir.FullName, "dic.txt"));
            UnityEditor.EditorApplication.isPlaying = false;
        }

    }
}
