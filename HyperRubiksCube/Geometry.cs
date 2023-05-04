using Geometry;
using Microsoft.Maui.Controls.Shapes;
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

    public Face3 Transform(Quaternion rotation, Vector3 translation)
    {
        var normal = Vector3.Transform(Normal, rotation);
        var vertices = Vertices
            .Select(v => Vector3.Transform(v, rotation))
            .Select(v => Vector3.Add(v, translation))
            .ToList();
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
        position = Vector3.Transform(position, Quaternion.Inverse(Orientation));

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

    public List<Face2> ProjectPolyhedron(Polyhedron cell)
    {
        return cell.Faces.Select(this.ProjectFace).Where(f => f != null).ToList();
    }

    public List<Face2> ProjectPolyhedrons(List<Polyhedron> cells)
    {
        return cells
            .OrderBy(c => c.Vertices.Select(v => -Vector3.Dot(v, Forward)).Max())
            .SelectMany(ProjectPolyhedron)
            .ToList();
    }
}

record Face3Indices(Vector3 Normal, List<Index> Vertices, Color Color)
{
    public Face3Indices Transform(Quaternion rotation)
    {
        return new Face3Indices(Vector3.Transform(Normal, rotation), Vertices, Color);
    }
}

record Polyhedron(List<Vector3> Vertices, List<Face3Indices> FaceIndices)
{
    public List<Face3> Faces
    {
        get
        {
            return FaceIndices.Select(
                faceIndices => new Face3(
                    Normal: faceIndices.Normal,
                    Vertices: faceIndices.Vertices.Select(i => Vertices[i]).ToList(),
                    Color: faceIndices.Color
                )
            ).ToList();
        }
    }

    public static Polyhedron Cube;

    static Polyhedron()
    {
        var cubeVertices = new List<Vector3>();
        var signs = new float[] { +1, -1 };
        foreach (var i in signs)
            foreach (var j in signs)
                foreach (var k in signs)
                    cubeVertices.Add(new(i, j, k));
        var cubeFaceIndices = new List<Face3Indices>()
        {
            new Face3Indices(new( 1, 0, 0), new List<Index> {
                4*0+2*0+1*0,
                4*0+2*1+1*0,
                4*0+2*1+1*1,
                4*0+2*0+1*1,
            }, Colors.Blue),
            new Face3Indices(new(-1, 0, 0), new List<Index> {
                4*1+2*0+1*0,
                4*1+2*0+1*1,
                4*1+2*1+1*1,
                4*1+2*1+1*0,
            }, Colors.Green),
            new Face3Indices(new( 0, 1, 0), new List<Index> {
                2*0+1*0+4*0,
                2*0+1*1+4*0,
                2*0+1*1+4*1,
                2*0+1*0+4*1,
            }, Colors.White),
            new Face3Indices(new( 0,-1, 0), new List<Index> {
                2*1+1*0+4*0,
                2*1+1*0+4*1,
                2*1+1*1+4*1,
                2*1+1*1+4*0,
            }, Colors.Yellow),
            new Face3Indices(new( 0, 0, 1), new List<Index> {
                1*0+4*0+2*0,
                1*0+4*1+2*0,
                1*0+4*1+2*1,
                1*0+4*0+2*1,
            }, Colors.Red),
            new Face3Indices(new( 0, 0,-1), new List<Index> {
                1*1+4*0+2*0,
                1*1+4*0+2*1,
                1*1+4*1+2*1,
                1*1+4*1+2*0,
            }, Colors.Orange),
        };

        Cube = new Polyhedron(cubeVertices, cubeFaceIndices);
    }

    public Polyhedron Transform(Quaternion rotation)
    {
        return new Polyhedron(
            Vertices.Select(v => Vector3.Transform(v, rotation)).ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList()
        );
    }

    public Polyhedron Transform(Quaternion rotation, Vector3 translation)
    {
        return new Polyhedron(
            Vertices
            .Select(v => Vector3.Transform(v, rotation))
            .Select(v => Vector3.Add(v, translation))
            .ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList()
        );
    }
}
