using Paths;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class Test : MonoBehaviour
{
    private void Start()
    {
        var path = Path.Create();
        path.AddPoint(Vector3.zero);
        path.AddPoint(new Vector3(1f, 0f, 0f));

        var point = new Point(2f, 0f, 0f, Quaternion.identity);

        path.InsertPoint(0, point);
        Debug.Log(path.ContainsPoint(point));

        var index = path.IndexOfPoint(Vector3.zero);
        Debug.Log(index);

        path.RemovePoint(point);
        path.RemovePointAt(index);

        Debug.Log(path.PointsCount);
    }
}
