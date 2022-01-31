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

    [SerializeField]
    [Range(0f, 1f)]
    private float _distance;

    private void Update()
    {
        Debug.Log(SceneView.lastActiveSceneView.camera.WorldToScreenPoint(transform.position));
    }
}
