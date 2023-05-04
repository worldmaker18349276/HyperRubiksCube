using System.Diagnostics;
using System.Numerics;

namespace Geometry;

record Face2(List<Vector2> Vertices, Color Color);

record Camera3(Quaternion Orientation, float FocalLength, float ScreenDistance)
{
    public Vector3 Looking
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
            return Vector3.Dot(position, Looking);

        var dis = position - FocalPoint;
        return dis.Length() * float.Sign(Vector3.Dot(dis, Looking));
    }

    public Face2 ProjectFace(Face3 face)
    {
        if (Vector3.Dot(face.Normal, Looking) > 0)
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

record Camera4(Matrix4x4 Orientation, float FocalLength, float ScreenDistance)
{
    public Vector4 Looking
    {
        get
        {
            return Vector4.Transform(new Vector4(0, 0, 0, -1), Orientation);
        }
    }

    public Vector4 Forward
    {
        get
        {
            return Vector4.Transform(new Vector4(0, 0, -1, 0), Orientation);
        }
    }

    public Vector4 Upward
    {
        get
        {
            return Vector4.Transform(new Vector4(0, 1, 0, 0), Orientation);
        }
    }

    public Vector4 FocalPoint
    {
        get
        {
            return Vector4.Transform(new Vector4(0, 0, 0, FocalLength), Orientation);
        }
    }

    public Camera4 Transform(Matrix4x4 rotation)
    {
        var orientation = rotation * Orientation;
        return this with { Orientation = orientation };
    }

    public Vector3 ProjectVector(Vector4 position)
    {
        Matrix4x4 inversedOrientation;
        var succ = Matrix4x4.Invert(Orientation, out inversedOrientation);
        Debug.Assert(succ);
        position = Vector4.Transform(position, inversedOrientation);

        if (float.IsInfinity(FocalLength))
            return new Vector3(position.X, position.Y, position.Z);

        var scale = (FocalLength - ScreenDistance) / (FocalLength - position.W);
        return new Vector3(position.X * scale, position.Y * scale, position.Z * scale);
    }

    public float ProjectionDistance(Vector4 position)
    {
        if (float.IsInfinity(FocalLength))
            return Vector4.Dot(position, Looking);

        var dis = position - FocalPoint;
        return dis.Length() * float.Sign(Vector4.Dot(dis, Looking));
    }

    public Cell3 ProjectCell(Cell4 cell)
    {
        if (Vector4.Dot(cell.Normal, Looking) > 0)
            return null;

        var vertices = cell.Vertices.Select(ProjectVector).ToList();
        var faceIndices = cell.FaceIndices
            .Select(f => new Face3Indices(ProjectVector(f.Normal), f.Vertices, cell.Color))
            .ToList();
        return new Cell3(vertices, faceIndices);
    }
}

record Face4Indices(Vector4 Normal, List<Index> Vertices)
{
    public Face4Indices Transform(Matrix4x4 rotation)
    {
        return new Face4Indices(Vector4.Transform(Normal, rotation), Vertices);
    }

}

record Cell4(Vector4 Normal, List<Vector4> Vertices, List<Face4Indices> FaceIndices, Color Color)
{
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

static class HyperCube
{
    public static List<Cell4> makeHyperCube(float cellHeight)
    {
        var cube = Cell3.Cube;

        return new List<Cell4> {
            new Cell4(
                Normal: new( 1, 0, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(1 + cellHeight, v.X, v.Y, v.Z))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(0, f.Normal.X, f.Normal.Y, f.Normal.Z), f.Vertices))
                    .ToList(),
                Color: Colors.Blue
            ),
            new Cell4(
                Normal: new(-1, 0, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(- 1 - cellHeight, v.Z, v.Y, v.X))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(0, f.Normal.Z, f.Normal.Y, f.Normal.X), f.Vertices))
                    .ToList(),
                Color: Colors.Green
            ),
            new Cell4(
                Normal: new( 0, 1, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, 1 + cellHeight, v.Z, v.Y))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.X, 0, f.Normal.Z, f.Normal.Y), f.Vertices))
                    .ToList(),
                Color: Colors.White
            ),
            new Cell4(
                Normal: new( 0,-1, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, -1 - cellHeight, v.X, v.Y))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Z, 0, f.Normal.X, f.Normal.Y), f.Vertices))
                    .ToList(),
                Color: Colors.Yellow
            ),
            new Cell4(
                Normal: new( 0, 0, 1, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Y, v.Z, 1 + cellHeight, v.X))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Y, f.Normal.Z, 0, f.Normal.X), f.Vertices))
                    .ToList(),
                Color: Colors.Red
            ),
            new Cell4(
                Normal: new( 0, 0,-1, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Y, v.X, -1 - cellHeight, v.Z))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Y, f.Normal.X, 0, f.Normal.Z), f.Vertices))
                    .ToList(),
                Color: Colors.Orange
            ),
            new Cell4(
                Normal: new( 0, 0, 0, 1),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, v.X, 1 + cellHeight))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Z, f.Normal.Y, f.Normal.X, 0), f.Vertices))
                    .ToList(),
                Color: Colors.Purple
            ),
            new Cell4(
                Normal: new( 0, 0, 0,-1),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, v.Z, -1 - cellHeight))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.X, f.Normal.Y, f.Normal.Z, 0), f.Vertices))
                    .ToList(),
                Color: Colors.Pink
            ),
        };
    }
}

static class Matrix4x4Extension
{
    public static Matrix4x4 CreateRotationXY(float angle)
    {
        var c = (float) Math.Cos(angle);
        var s = (float) Math.Sin(angle);
        return new Matrix4x4(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f,  c, -s,
            0f, 0f,  s,  c
        );
    }
}
