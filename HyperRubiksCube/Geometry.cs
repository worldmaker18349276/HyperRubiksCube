using System.Linq;
using System.Numerics;

namespace Geometry;

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

    public Vector3 FocalPoint
    {
        get
        {
            return Vector3.Transform(new Vector3(0, 0, FocalLength), Orientation);
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

        var scale = (FocalLength - ScreenDistance) / (FocalLength - position.Z);
        return new Vector2(position.X * scale, position.Y * scale);
    }

    public float ProjectionDistance(Vector3 position)
    {
        if (float.IsInfinity(FocalLength))
            return Vector3.Dot(position, Forward);

        var dis = position - FocalPoint;
        return dis.Length() * float.Sign(Vector3.Dot(dis, Forward));
    }

    public Face2 ProjectFace(Face3 face)
    {
        if (Vector3.Dot(face.Normal, Forward) > 0)
            return null;

        var vertices = face.Vertices.Select(ProjectVector).ToList();
        return new Face2(vertices, face.Color);
    }

    public List<Face2> ProjectPolyhedron(Cell3 cell)
    {
        return cell.Faces.Select(ProjectFace).Where(f => f != null).ToList();
    }

    public List<Face2> ProjectPolyhedrons(List<Cell3> cells)
    {
        var faces = cells
            .OrderByDescending(cell => cell.Vertices.Select(ProjectionDistance).Max())
            .SelectMany(cell =>
                cell.Faces.Select(f => (f, ProjectFace(f))).Where(f => f.Item2 != null)
            )
            .ToList();

        return shadowFaces(faces);
    }

    List<Face2> shadowFaces(List<(Face3, Face2)> faces)
    {
        // TODO: shadow faces by projection distances
        return faces.Select(f => f.Item2).ToList();
    }
}

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
            .Select(v => Vector3.Transform(v, rotation) + translation)
            .ToList();
        return new Face3(normal, vertices, Color);
    }
}

record Face3Indices(Vector3 Normal, List<Index> Vertices, Color Color)
{
    public Face3Indices Transform(Quaternion rotation)
    {
        return new Face3Indices(Vector3.Transform(Normal, rotation), Vertices, Color);
    }
}

record Cell3(List<Vector3> Vertices, List<Face3Indices> FaceIndices)
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

    public static Cell3 Cube;

    static Cell3()
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

        Cube = new Cell3(cubeVertices, cubeFaceIndices);
    }

    public Cell3 Transform(Quaternion rotation)
    {
        return new Cell3(
            Vertices.Select(v => Vector3.Transform(v, rotation)).ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList()
        );
    }

    public Cell3 Transform(Quaternion rotation, Vector3 translation)
    {
        return new Cell3(
            Vertices
            .Select(v => Vector3.Transform(v, rotation) + translation)
            .ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList()
        );
    }
}

record Face4(Vector4 Normal1, Vector4 Normal2, List<Vector4> Vertices, Color Color)
{
    public Face4 Transform(Matrix4x4 rotation)
    {
        var normal1 = Vector4.Transform(Normal1, rotation);
        var normal2 = Vector4.Transform(Normal2, rotation);
        var vertices = Vertices.Select(v => Vector4.Transform(v, rotation)).ToList();
        return new Face4(normal1, normal2, vertices, Color);
    }

    public Face4 Transform(Matrix4x4 rotation, Vector4 translation)
    {
        var normal1 = Vector4.Transform(Normal1, rotation);
        var normal2 = Vector4.Transform(Normal2, rotation);
        var vertices = Vertices
            .Select(v => Vector4.Transform(v, rotation) + translation)
            .ToList();
        return new Face4(normal1, normal2, vertices, Color);
    }
}

record Face4Indices(Vector4 Normal1, Vector4 Normal2, List<Index> Vertices)
{
    public Face4Indices Transform(Matrix4x4 rotation)
    {
        return new Face4Indices(Vector4.Transform(Normal1, rotation), Vector4.Transform(Normal2, rotation), Vertices);
    }

}

record Cell4(Vector4 Normal, List<Vector4> Vertices, List<Face4Indices> FaceIndices, Color Color)
{
    public List<Face4> Faces
    {
        get
        {
            return FaceIndices.Select(
                faceIndices => new Face4(
                    Normal1: faceIndices.Normal1,
                    Normal2: faceIndices.Normal2,
                    Vertices: faceIndices.Vertices.Select(i => Vertices[i]).ToList(),
                    Color: Color
                )
            ).ToList();
        }
    }

    public Cell4 Transform(Matrix4x4 rotation)
    {
        return new Cell4(
            Vector4.Transform(Normal, rotation),
            Vertices.Select(v => Vector4.Transform(v, rotation)).ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList(),
            Color
        );
    }

    public Cell4 Transform(Matrix4x4 rotation, Vector4 translation)
    {
        return new Cell4(
            Vector4.Transform(Normal, rotation),
            Vertices
            .Select(v => Vector4.Transform(v, rotation) + translation)
            .ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList(),
            Color
        );
    }
}
