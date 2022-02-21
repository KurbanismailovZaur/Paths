using Paths;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    private float _height;

    [SerializeField]
    private float _frequency;

    [SerializeField]
    private int _repeat;

    [SerializeField]
    private bool _startToUp;

    private void Start()
    {
        var path = Path.CreateWave(Vector3.zero, Vector3.back, Vector3.up, _height, _frequency, _repeat, _startToUp);
    }
}
