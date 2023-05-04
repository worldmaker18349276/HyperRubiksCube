using System.Numerics;

namespace Geometry;

record Face3(Vector3 Normal, List<Vector3> Vertices, Color Color)
{
    public Face3 Transform(Quaternion rotation)
    {
        var normal = Vector3.Transform(Normal, rotation);
        var vertices = Vertices.Select(v => Vector3.Transform(v, rotation)).ToList();
        return new Face3(normal, vertices, Color);
    }
}

record Face2(List<Vector2> Vertices, Color Color);

record Camera3(Quaternion Orientation, float FocalLength, float ScreenDistance)
{
    public Vector3 Forward
    {
        get
        {
            return Vector3.Transform(new Vector3(0, 0, -1), Orientation);
        }
    }

    public Vector3 Upward
    {
        get
        {
            return Vector3.Transform(new Vector3(0, 1, 0), Orientation);
        }
    }

    public Camera3 Transform(Quaternion rotation)
    {
        var orientation = rotation * Orientation;
        return this with { Orientation = orientation };
    }

    public Vector2 ProjectVector(Vector3 position)
    {
        position = Vector3.Transform(position, Orientation);

        if (float.IsInfinity(FocalLength))
            return new Vector2(position.X, position.Y);

        var scale = (FocalLength - ScreenDistance) / (FocalLength - position.X);
        return new Vector2(position.X * scale, position.Y * scale);
    }

    public Face2 ProjectFace(Face3 face)
    {
        if (Vector3.Dot(face.Normal, Forward) > 0)
            return null;

        var vertices = face.Vertices.Select(this.ProjectVector).ToList();
        return new Face2(vertices, face.Color);
    }
}
