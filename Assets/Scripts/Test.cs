using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    private Paths.Path _path;

    [SerializeField]
    [Range(0f, 10f)]
    private float _distance;

    [SerializeField]
    private bool _useNormalizedDistance = true;

    private void Update()
    {
        //yield return new WaitForSeconds(1f);

        print(_path.GetPoint(1, _distance, _useNormalizedDistance));
    }
}
