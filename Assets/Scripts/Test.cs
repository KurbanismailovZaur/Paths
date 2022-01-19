using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    private Paths.Path _path;

    [SerializeField]
    private int _segment;

    [SerializeField]
    [Range(-1f, 10f)]
    private float _distance;

    [SerializeField]
    private bool _useNormalizedDistance = true;

    private void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        var position = _path.GetPoint(_segment, _distance, _useNormalizedDistance);
        Gizmos.DrawSphere(position, 0.05f);
    }
}
