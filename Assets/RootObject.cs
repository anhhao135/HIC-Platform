using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]

public class RootObject : MonoBehaviour
{

    public Vector3[] BoundingBox_WorldCoord;
    public int uniqueID;
    public string className;
    public Vector3 boundingBoxCenter;
    public float boundingBoxXSize;
    public float boundingBoxYSize;
    public float boundingBoxZSize;
    public Vector3 boundingBoxForwardDirectionInWorld;
    public Vector3 boundingBoxCenterWorldSpace;

    // Start is called before the first frame update


    void Start()
    {
        uniqueID = Mathf.Abs(GetInstanceID());

        if (GetComponent<BoxCollider>() != null)
        {
            BoxCollider col = GetComponent<BoxCollider>();

            Vector3[] pts3D = new Vector3[8];

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

            BoundingBox_WorldCoord = pts3D;

            boundingBoxCenter = col.center;
            boundingBoxXSize = Vector3.Magnitude(pts3D[5] - pts3D[1]);
            boundingBoxYSize = Vector3.Magnitude(pts3D[3] - pts3D[1]); ;
            boundingBoxZSize = Vector3.Magnitude(pts3D[1] - pts3D[0]); ;

        }

        boundingBoxForwardDirectionInWorld = transform.forward;

        boundingBoxCenterWorldSpace = transform.TransformPoint(boundingBoxCenter);
    }
    void FixedUpdate()
    {

        if (GetComponent<BoxCollider>() != null)
        {
            BoxCollider col = GetComponent<BoxCollider>();

            Vector3[] pts3D = new Vector3[8];

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

            BoundingBox_WorldCoord = pts3D;

            boundingBoxCenter = col.center;
            boundingBoxXSize = Vector3.Magnitude(pts3D[5] - pts3D[1]);
            boundingBoxYSize = Vector3.Magnitude(pts3D[3] - pts3D[1]); ;
            boundingBoxZSize = Vector3.Magnitude(pts3D[1] - pts3D[0]); ;

        }

        boundingBoxForwardDirectionInWorld = transform.forward;

        boundingBoxCenterWorldSpace = transform.TransformPoint(boundingBoxCenter);

    }


    void OnDrawGizmos()
    {
        for (int i = 0; i < BoundingBox_WorldCoord.Length; i++)
        {

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(BoundingBox_WorldCoord[i], 0.1f);
            Handles.Label(BoundingBox_WorldCoord[i] + Vector3.up, i.ToString());
            Handles.Label(transform.position + Vector3.up * 3f, uniqueID.ToString());
            Handles.Label(transform.position + Vector3.up * 2.5f, className);

        }

        Gizmos.DrawSphere(boundingBoxCenterWorldSpace, 0.1f);
    }


}
