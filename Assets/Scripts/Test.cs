using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    private Paths.Path _path;

    [SerializeField]
    private List<int> _ints;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);
        _path.Points.RemoveAt(1);
        _path.Points.Insert(1, Vector3.one);

        yield return new WaitForSeconds(1f);
        _path.Points.Add(Vector3.one);

        yield return new WaitForSeconds(1f);
        _path.Points.RemoveAt(1);

        yield return new WaitForSeconds(1f);
        _path.Points.Clear();

        yield return new WaitForSeconds(1f);
        _path.Points.AddRange(new Vector3[] { Vector3.zero, Vector3.right, Vector3.one, Vector3.left });
        //_ints.Clear();
    }
}
