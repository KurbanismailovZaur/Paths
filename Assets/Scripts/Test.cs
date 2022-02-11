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

    private void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            Application.targetFrameRate = 90;
        }
    }
    private void Update()
    {
        var t = Mathf.PingPong(Time.time * 0.25f, 1f);
        var data = _path.GetPoint(t);

        transform.position = data.Position;
        transform.rotation = Quaternion.LookRotation(data.Direction);
    }

    //private void Update()
    //{
    //    var t = Mathf.PingPong(Time.time, 1f);
    //    var posData = _path.GetPoint(t);

    //    transform.position = posData.Position;
    //    transform.rotation = Quaternion.LookRotation(posData.Direction);
    //}
}
