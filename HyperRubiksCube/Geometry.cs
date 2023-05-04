using System.Numerics;

namespace Geometry;

class Face3
{
    public Vector3 Normal { get; set; }
    public List<Vector3> Vertices { get; set; }
    public Color Color { get; set; }

    public Face3(Vector3 normal, List<Vector3> vertices, Color color)
    {
        Normal = normal;
        Vertices = vertices;
        Color = color;
    }

    public void Transform(Quaternion rotation)
    {
        Normal = Vector3.Transform(Normal, rotation);
        Vertices = Vertices.Select(v => Vector3.Transform(v, rotation)).ToList();
    }
}

class Face2
{
    public List<Vector2> Vertices { get; set; }
    public Color Color { get; set; }

    public Face2(List<Vector2> vertices, Color color)
    {
        Vertices = vertices;
        Color = color;
    }
}

class Camera3
{
    public Quaternion Orientation { get; set; }
    public float FocalLength { get; set; }
    public float ScreenDistance { get; set; }

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

    public Camera3(Quaternion orientation, float focalLength, float screenDistance)
    {
        Orientation = orientation;
        FocalLength = focalLength;
        ScreenDistance = screenDistance;
    }

    public void Transform(Quaternion rotation)
    {
        Orientation = rotation * Orientation;
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
