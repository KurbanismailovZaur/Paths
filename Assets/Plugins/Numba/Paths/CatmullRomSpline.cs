using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Tweens
{
	public class CatmullRomSpline : MonoBehaviour
	{
		public bool isLooping = true;

		public float resolution = 1f;

		void OnDrawGizmos()
		{
			Gizmos.color = Color.yellow;

			for (int i = 0; i < transform.childCount; i++)
			{
				if ((i == 0 || i == transform.childCount - 2 || i == transform.childCount - 1) && !isLooping)
					continue;

				DisplayCatmullRomSpline(i);
			}
		}

		void DisplayCatmullRomSpline(int pos)
		{
			Vector3 p0 = transform.GetChild(ClampListPos(pos - 1)).position;
			Vector3 p1 = transform.GetChild(pos).position;
			Vector3 p2 = transform.GetChild(ClampListPos(pos + 1)).position;
			Vector3 p3 = transform.GetChild(ClampListPos(pos + 2)).position;

			Vector3 lastPos = p1;

			int steps = Mathf.FloorToInt(1f / resolution);

			for (int i = 1; i <= steps; i++)
			{
				float t = i * resolution;
				Vector3 newPos = GetCatmullRomPosition(t, p0, p1, p2, p3);

				Gizmos.DrawLine(lastPos, newPos);
				lastPos = newPos;
			}
		}

		int ClampListPos(int pos)
		{
			if (pos < 0)
				pos = transform.childCount - 1;

			if (pos > transform.childCount)
				pos = 1;
			else if (pos > transform.childCount - 1)
				pos = 0;

			return pos;
		}

		/// <summary>
		/// Calculate CatmullRom position.
		/// <br/> See <see href="https://www.habrador.com/tutorials/interpolation/1-catmull-rom-splines/"/>
		/// </summary>
		/// <param name="t">Value between <see langword="0"/> and <see langword="1"/> which represent normalized distance from <paramref name="p1"/> to <paramref name="p2"/> when draw line.</param>
		/// <param name="p0">Start control point.</param>
		/// <param name="p1">Line start position.</param>
		/// <param name="p2">Line end position.</param>
		/// <param name="p3">End control point.</param>
		/// <returns>Calculated line point.</returns>
		Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
		{
			Vector3 a = 2f * p1;
			Vector3 b = p2 - p0;
			Vector3 c = 2f * p0 - 5f * p1 + 4f * p2 - p3;
			Vector3 d = -p0 + 3f * p1 - 3f * p2 + p3;

			Vector3 pos = 0.5f * (a + (b * t) + (t * t * c) + (t * t * t * d));

			return pos;
		}
	}
}