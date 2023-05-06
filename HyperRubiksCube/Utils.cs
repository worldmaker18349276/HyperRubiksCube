using System.Diagnostics;
using System.Numerics;

namespace Utils;

static class Algorithm
{
    public static List<T> TopoSort<T>(List<T> elems, Func<T, T, int> hasEdge)
    {
        var edges = new HashSet<(int, int)>();
        foreach (var i in Enumerable.Range(0, elems.Count - 1))
            foreach (var j in Enumerable.Range(i + 1, elems.Count - i - 1))
            {
                var d = hasEdge(elems[i], elems[j]);
                if (d == 0)
                    continue;
                edges.Add(d < 0 ? (i, j) : (j, i));
            }

        var res = new List<T>();
        var notMin = edges.Select(ij => ij.Item2).ToHashSet();
        var min = Enumerable.Range(0, elems.Count)
            .Where(i => !notMin.Contains(i))
            .ToHashSet();

        while (min.Count > 0)
        {
            var i = min.Min();
            min.Remove(i);
            res.Add(elems[i]);

            var next = edges.Where(e => e.Item1 == i).Select(e => e.Item2).ToHashSet();

            // trim all edges to minimal elements
            edges.RemoveWhere(e => e.Item1 == i);
            var notMin_ = edges.Select(ij => ij.Item2).ToHashSet();

            // find new minimal elements after trimming
            foreach (var j in next)
                if (!notMin_.Contains(j))
                    min.Add(j);
        }

        // Debug.Assert(res.Count == elems.Count);
        return res;
    }
}

static class Matrix4x4Extension
{
    public static Matrix4x4 CreateRotationXY(float angle)
    {
        var c = (float)Math.Cos(angle);
        var s = (float)Math.Sin(angle);
        return new Matrix4x4(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, c, -s,
            0f, 0f, s, c
        );
    }

    public static Matrix4x4 CreateFromYawPitchRollGyro(float yaw, float pitch, float roll, float gyro)
    {
        return Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll) * CreateRotationXY(gyro);
    }

    public static Matrix4x4 RotateToXY((Vector4, Vector4) axis)
    {
        var axis1 = axis.Item1;
        var axis2 = axis.Item2;

        // 1. rotate one of axis to XYZ subspace without consider another one
        // 1.1. choose a proper one to rotate first, which should have smaller W component.
        bool flipped = axis1.W > axis2.W;
        if (flipped)
            (axis1, axis2) = (axis2, axis1);

        // 1.2. project axis1 to XYZ subspace
        var axis1Proj = new Vector3(axis1.X, axis1.Y, axis1.Z);

        // 1.3. rotate axis1Proj to Z axis under XYZ subspace
        var rotation1 = Matrix4x4.CreateLookAt(new(0, 0, 0), -axis1Proj, new(0, 0, 1));
        axis1 = Vector4.Transform(axis1, rotation1);
        axis2 = Vector4.Transform(axis2, rotation1);

        // 1.4. rotate along ZW plane, which should rotate axis1 to Z axis
        var rotation2 = CreateRotationXY((float)-Math.Atan2(axis1.W, axis1.Z));
        axis1 = Vector4.Transform(axis1, rotation2);
        axis2 = Vector4.Transform(axis2, rotation2);
        Debug.Assert(Vector4.Distance(axis1, new(0, 0, 1, 0)) < 0.001);

        // 2. rotate another axis to XYZ subspace by the first one
        // 2.1. rotate along ZW plane 90 degree, so that `axis2.W == 0`
        var rotation3 = CreateRotationXY(-float.Pi / 2);
        axis1 = Vector4.Transform(axis1, rotation3);
        axis2 = Vector4.Transform(axis2, rotation3);
        Debug.Assert(Vector4.Distance(axis1, new(0, 0, 0, 1)) < 0.001);

        // 2.2. rotate axis2 to Z axis
        var axis2Proj = new Vector3(axis2.X, axis2.Y, axis2.Z);
        var rotation4 = Matrix4x4.CreateLookAt(new(0, 0, 0), -axis2Proj, new(0, 0, 1));
        axis1 = Vector4.Transform(axis1, rotation4);
        axis2 = Vector4.Transform(axis2, rotation4);
        Debug.Assert(Vector4.Distance(axis1, new(0, 0, 0, 1)) < 0.001);
        Debug.Assert(Vector4.Distance(axis2, new(0, 0, 1, 0)) < 0.001);

        // 3. rotate axis1 and axis2 to XY plane
        Matrix4x4 rotation5;
        if (flipped)
            rotation5 =
                Matrix4x4.CreateRotationY(float.Pi / 2)
                * CreateRotationXY(float.Pi / 2)
                * Matrix4x4.CreateRotationX(-float.Pi / 2);
        else
            rotation5 =
                Matrix4x4.CreateRotationX(-float.Pi / 2)
                * CreateRotationXY(float.Pi / 2)
                * Matrix4x4.CreateRotationY(float.Pi / 2);

        axis1 = Vector4.Transform(axis1, rotation5);
        axis2 = Vector4.Transform(axis2, rotation5);
        if (flipped)
            (axis1, axis2) = (axis2, axis1);
        Debug.Assert(Vector4.Distance(axis1, new(1, 0, 0, 0)) < 0.001);
        Debug.Assert(Vector4.Distance(axis2, new(0, 1, 0, 0)) < 0.001);

        return (rotation1 * rotation2 * rotation3 * rotation4 * rotation5).Normalize();
    }

    public static Matrix4x4 Create4DRotationFromAxisAngle((Vector4, Vector4) axis, float angle)
    {
        var rotation1 = RotateToXY(axis);
        var rotation2 = CreateRotationXY(angle);
        Matrix4x4 rotation3;
        bool succ = Matrix4x4.Invert(rotation1, out rotation3);
        Debug.Assert(succ);
        return (rotation1 * rotation2 * rotation3).Normalize();
    }

    public static Matrix4x4 Normalize(this Matrix4x4 mat)
    {
        var det = mat.GetDeterminant();
        return mat * (float)(1 / Math.Sqrt(Math.Sqrt(det)));
    }
}
