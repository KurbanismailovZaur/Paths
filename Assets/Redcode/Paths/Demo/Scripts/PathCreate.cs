using UnityEngine;

namespace Redcode.Paths.Demo
{
    public class PathCreate : MonoBehaviour
    {
        private void Start()
        {
            var path = Path.Create();
            path.AddPoint(Vector3.zero);
            path.AddPoint(new Vector3(1f, 2f, 0f));
            path.AddPoint(new Vector3(2f, 0f, 0f));
            path.AddPoint(new Vector3(0f, 1.25f, 0f));
            path.AddPoint(new Vector3(2f, 1.25f, 0f));

            path.Looped = true;
        }
    }
}
