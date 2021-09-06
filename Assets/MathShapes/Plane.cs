using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plane
{
    public Vector3 Normal { get; }
    public float Dist { get; }

    public Plane(Vector3 a, Vector3 b, Vector3 c)
    {
        Normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        Dist = Vector3.Dot(Normal, a);
    }

    public (bool, float) TestSegment(Vector3 p0, Vector3 p1)
    {
        Vector3 p01 = p1 - p0;
        float t = (Dist - Vector3.Dot(Normal, p0)) / Vector3.Dot(Normal, p01);

        return (t >= 0.0f && t <= 1.0f, t);
    }
    
}
