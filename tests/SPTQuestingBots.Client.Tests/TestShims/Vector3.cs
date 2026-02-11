namespace UnityEngine;

/// <summary>
/// Minimal Vector3 shim for testing PositionHistory without Unity assemblies.
/// Only implements the subset used by PositionHistory.
/// </summary>
public struct Vector3
{
    public float x;
    public float y;
    public float z;

    public Vector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public static Vector3 zero
    {
        get { return new Vector3(0, 0, 0); }
    }

    public float sqrMagnitude
    {
        get { return x * x + y * y + z * z; }
    }

    public float magnitude
    {
        get { return (float)System.Math.Sqrt(sqrMagnitude); }
    }

    public static Vector3 operator -(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }
}
