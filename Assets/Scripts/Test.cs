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
        print($"{Quaternion.Euler(new Vector3(9.000001f, 0f, 0f)).eulerAngles.x:0.0000000}"); 
    }

    //private void Update()
    //{
    //    var t = Mathf.PingPong(Time.time, 1f);
    //    var posData = _path.GetPoint(t);

    //    transform.position = posData.Position;
    //    transform.rotation = Quaternion.LookRotation(posData.Direction);
    //}
}
