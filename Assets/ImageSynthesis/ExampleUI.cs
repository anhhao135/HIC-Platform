using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEditor;
using System.IO;

[RequireComponent (typeof(ImageSynthesis))]
public class ExampleUI : MonoBehaviour {

	public int width = 1920;
	public int height = 1080;
	private int imageCounter = 1;
	private string sceneName;
	public ImageSynthesis IS;
	private DirectoryInfo dataset;


	public void Start()
	{
		sceneName = SceneManager.GetActiveScene().name; 
		IS = GetComponent<ImageSynthesis>();
		dataset = Directory.CreateDirectory(string.Format("DataSet_{0:yyyy-MM-dd_hh-mm-ss-tt}", System.DateTime.Now));
	}
	void OnGUI ()
	{
		if (GUILayout.Button("Captcha!!! (" + imageCounter + ")"))
		{
			var sceneName = SceneManager.GetActiveScene().name;
			// NOTE: due to per-camera / per-object motion being calculated late in the frame and after Update()
			// capturing is moved into LateUpdate (see ImageSynthesis.cs Known Issues)
			GetComponent<ImageSynthesis>().Save(sceneName + "_" + imageCounter++, width, height);
		}
	}

	public void Update()
	{
		IS.OnSceneChange();
		
		IS.Save(sceneName + "_" + imageCounter++, width, height, dataset.FullName);

		if (imageCounter > 20)
		{
			EditorApplication.isPlaying = false;
		}
	}
}
