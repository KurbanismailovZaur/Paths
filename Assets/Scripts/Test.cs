using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    private List<int> _ints;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1f);

        //FindObjectOfType<Paths.Path>().Points;
        //_ints.Clear();
    }
}
