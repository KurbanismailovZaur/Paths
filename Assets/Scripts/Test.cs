using Paths;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        transform.position = _path.GetPoint(_distance);
    }
}
