using Geometry;
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

    public List<Face2> ProjectFaces(List<Face3> faces)
    {
        return faces.Select(this.ProjectFace).Where(f => f != null).ToList();
    }
}

static class Polyhedron
{
    public static List<Face3> Cube = new List<Face3>
        {
            new Face3(new( 1, 0, 0), new List<Vector3> {new( 1, 1, 1), new( 1,-1, 1), new( 1,-1,-1), new( 1, 1,-1)}, Colors.Blue),
            new Face3(new(-1, 0, 0), new List<Vector3> {new(-1, 1, 1), new(-1, 1,-1), new(-1,-1,-1), new(-1,-1, 1)}, Colors.Green),
            new Face3(new( 0, 1, 0), new List<Vector3> {new( 1, 1, 1), new( 1, 1,-1), new(-1, 1,-1), new(-1, 1, 1)}, Colors.Ivory),
            new Face3(new( 0,-1, 0), new List<Vector3> {new( 1,-1, 1), new(-1,-1, 1), new(-1,-1,-1), new( 1,-1,-1)}, Colors.DimGray),
            new Face3(new( 0, 0, 1), new List<Vector3> {new( 1, 1, 1), new(-1, 1, 1), new(-1,-1, 1), new( 1,-1, 1)}, Colors.Red),
            new Face3(new( 0, 0,-1), new List<Vector3> {new( 1, 1,-1), new( 1,-1,-1), new(-1,-1,-1), new(-1, 1,-1)}, Colors.Orange),
        };
}
