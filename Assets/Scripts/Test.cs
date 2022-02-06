using Paths;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    private Path _path;

    private void Update()
    {
        var t = Mathf.PingPong(Time.time, 1f);
        var posData = _path.GetPoint(t);

        transform.position = posData.Position;
        transform.rotation = Quaternion.LookRotation(posData.Direction);
    }
}
