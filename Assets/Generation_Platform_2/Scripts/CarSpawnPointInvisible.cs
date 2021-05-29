using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarSpawnPointInvisible : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        BoxCollider[] boxColliders = GetComponentsInChildren<BoxCollider>();

        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            meshRenderer.enabled = false;
        }

        foreach (BoxCollider boxCollider in boxColliders)
        {
            boxCollider.enabled = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
