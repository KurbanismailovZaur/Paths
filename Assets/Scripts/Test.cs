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
        var path = Path.CreatePolygon(4, 1f);
        var data = path.GetPoint(0);
        print(data);
    }
}
