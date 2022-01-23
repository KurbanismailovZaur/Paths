using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    private void Awake() => _path = GetComponent<Paths.Path>();

    //private void OnDrawGizmosSelected()
    //{
    //    var position = _path.GetPoint(_segment, _distance, true, true);
    //    Gizmos.DrawSphere(position, 0.1f);
    //}

    private IEnumerator Start()
    {
        while (true)
        {
            _path.OptimizeResolutionByAngle(1f);

            yield return new WaitForSeconds(1f);
        }
    }
}
