using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AABB
{
    public Vector3 Min { get; }
    public Vector3 Max { get; }

    public const float Epsilon = 0.001f;

    public AABB(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public bool TestSegment(Vector3 p0, Vector3 p1)
    {
        Vector3 c = (Min + Max) * 0.5f;
        Vector3 e = Max - c;
        Vector3 m = (p0 + p1) * 0.5f;
        Vector3 d = p1 - m;
        m = m - c;

        float adx = Math.Abs(d.x);
        if (Math.Abs(m.x) > e.x + adx) return false;
        float ady = Math.Abs(d.y);
        if (Math.Abs(m.y) > e.y + ady) return false;
        float adz = Math.Abs(d.z);
        if (Math.Abs(m.z) > e.z + adz) return false;

        adx += Epsilon;
        ady += Epsilon;
        adz += Epsilon;

        if (Math.Abs(m.y * d.z - m.z * d.y) > e.y * adz + e.z * ady) return false;
        if (Math.Abs(m.z * d.x - m.x * d.z) > e.x * adz + e.z * adx) return false;
        if (Math.Abs(m.x * d.y - m.y * d.x) > e.x * ady + e.y * adx) return false;

        return true;
    }

    public (bool, Vector3) IntersectRay(Ray ray, float dist)
    {
        float tmin = 0.0f;
        float tmax = dist;

        float[] p = {ray.origin.x, ray.origin.y, ray.origin.z};
        float[] d = {ray.direction.x, ray.direction.y, ray.direction.z};
        float[] min = {Min.x, Min.y, Min.z};
        float[] max = {Max.x, Max.y, Max.z};
        
        for (int i = 0; i < 3; i++)
        {
            if (Math.Abs(d[i]) < Epsilon)
            {
                if (p[i] < min[i] || p[i] > max[i])
                {
                    return (false, new Vector3(-1, -1, -1));
                }
            }
            else
            {
                float ood = 1.0f / d[i];
                float t1 = (min[i] - p[i]) * ood;
                float t2 = (max[i] - p[i]) * ood;
                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                }

                tmin = Math.Max(tmin, t1);
                tmax = Math.Min(tmax, t2);
                if (tmin > tmax)
                {
                    return (false, new Vector3(-1, -1, -1));
                }
            }
        }

        return (true, ray.origin + ray.direction * tmin);
    }

    public List<AABB> Subdivide()
    {
        List<AABB> subList = new List<AABB>(8);

        Vector3 mid = (Max - Min) / 2.0f;
        
        Vector3 min0 = Min + new Vector3(0.0f, 0.0f, 0.0f);
        Vector3 min1 = Min + new Vector3(mid.x, 0.0f, 0.0f);
        Vector3 min2 = Min + new Vector3(mid.x, 0.0f, mid.z);
        Vector3 min3 = Min + new Vector3(0.0f, 0.0f, mid.z);
        Vector3 min4 = Min + new Vector3(0.0f,  mid.y, 0.0f);
        Vector3 min5 = Min + new Vector3(mid.x, mid.y, 0.0f);
        Vector3 min6 = Min + new Vector3(mid.x, mid.y, mid.z);
        Vector3 min7 = Min + new Vector3(0.0f,  mid.y, mid.z);
        
        Vector3 max0 = Min + mid + new Vector3(0.0f, 0.0f, 0.0f);
        Vector3 max1 = Min + mid + new Vector3(mid.x, 0.0f, 0.0f);
        Vector3 max2 = Min + mid + new Vector3(mid.x, 0.0f, mid.z);
        Vector3 max3 = Min + mid + new Vector3(0.0f, 0.0f, mid.z);
        Vector3 max4 = Min + mid + new Vector3(0.0f,  mid.y, 0.0f);
        Vector3 max5 = Min + mid + new Vector3(mid.x, mid.y, 0.0f);
        Vector3 max6 = Min + mid + new Vector3(mid.x, mid.y, mid.z);
        Vector3 max7 = Min + mid + new Vector3(0.0f,  mid.y, mid.z);
        
        subList.Add(new AABB(min0, max0));
        subList.Add(new AABB(min1, max1));
        subList.Add(new AABB(min2, max2));
        subList.Add(new AABB(min3, max3));
        subList.Add(new AABB(min4, max4));
        subList.Add(new AABB(min5, max5));
        subList.Add(new AABB(min6, max6));
        subList.Add(new AABB(min7, max7));

        return subList;
    }

    public float CubicSize()
    {
        Vector3 size = Max - Min;
        return size.x * size.y * size.z;
    }
}
