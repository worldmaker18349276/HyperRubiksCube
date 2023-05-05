using System.Diagnostics;
using System.Numerics;

namespace Geometry;

record Face2(List<Vector2> Vertices, Color Color);

class Camera3
{
    public Quaternion Orientation { get; set; }
    public float FocalLength { get; set; }
    public float ScreenDistance { get; set; }

    public Camera3(Quaternion orientation, float focalLength, float screenDistance)
    {
        Orientation = orientation;
        FocalLength = focalLength;
        ScreenDistance = screenDistance;
    }

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

    public Vector2 ProjectPosition(Vector3 position)
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
        // if (Vector3.Dot(face.Normal, Looking) > 0)
        //     return null;
        if (Vector3.Dot(face.ComputeNormal(), Looking) > 0)
            return null;

        var vertices = face.Vertices.Select(ProjectPosition).ToList();
        return new Face2(vertices, face.Color);
    }

    public List<Face2> ProjectCell(Cell3 cell)
    {
        return cell.Faces.Select(ProjectFace).Where(f => f != null).ToList();
    }

    public List<Face2> ProjectCells(List<Cell3> cells)
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
    public Vector3 ComputeNormal()
    {
        var v = new List<Vector3>();
        v.Add(Vertices[0] - Vertices[Vertices.Count - 1]);
        for (var i = 1; i < Vertices.Count; i++)
            v.Add(Vertices[i] - Vertices[i - 1]);
        var w = new Vector3(0, 0, 0);
        w += Vector3.Cross(v[Vertices.Count - 1], v[0]);
        for (var i = 1; i < Vertices.Count; i++)
            w += Vector3.Cross(v[i - 1], v[i]);

        return w;
    }

    public Face3 Transform(float scale)
    {
        var vertices = Vertices.Select(v => v * scale).ToList();
        return new Face3(Normal, vertices, Color);
    }

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

    public Cell3 Transform(float scale)
    {
        return new Cell3(
            Vertices.Select(v => v * scale).ToList(),
            FaceIndices
        );
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

class Camera4
{
    public Matrix4x4 Orientation { get; set; }
    public float FocalLength { get; set; }
    public float ScreenDistance { get; set; }

    public Camera4(Matrix4x4 orientation, float focalLength, float screenDistance)
    {
        Orientation = orientation;
        FocalLength = focalLength;
        ScreenDistance = screenDistance;
    }

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

    public Vector3 ProjectPosition(Vector4 position)
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

    public Vector3 ProjectTensor((Vector4, Vector4) tensor)
    {
        // left contraction: -Looking |_ tensor
        Matrix4x4 inversedOrientation;
        var succ = Matrix4x4.Invert(Orientation, out inversedOrientation);
        Debug.Assert(succ);
        var normal1 = Vector4.Transform(tensor.Item1, inversedOrientation);
        var normal2 = Vector4.Transform(tensor.Item2, inversedOrientation);
        var normal = new Vector3(
            normal1.W * normal2.X - normal2.W * normal1.X,
            normal1.W * normal2.Y - normal2.W * normal1.Y,
            normal1.W * normal2.Z - normal2.W * normal1.Z
        );

        return Vector3.Normalize(normal);
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

        var vertices = cell.Vertices.Select(ProjectPosition).ToList();
        var faceIndices = cell.FaceIndices
            .Select(f => new Face3Indices(ProjectTensor((cell.Normal, f.Normal)), f.Vertices, cell.Color))
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
    public Cell4 Transform(float scale)
    {
        return new Cell4(
            Normal,
            Vertices.Select(v => v * scale).ToList(),
            FaceIndices,
            Color
        );
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

static class HyperCube
{
    public static List<Cell4> makeHyperCube(float cellHeight)
    {
        var cube = Cell3.Cube;

        return new List<Cell4> {
            new Cell4(
                Normal: new( 1, 0, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(1 + cellHeight, v.Z, v.Y, v.X))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(0, f.Normal.Z, f.Normal.Y, f.Normal.X), f.Vertices))
                    .ToList(),
                Color: Colors.Blue
            ),
            new Cell4(
                Normal: new(-1, 0, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(- 1 - cellHeight, v.X, v.Y, v.Z))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(0, f.Normal.X, f.Normal.Y, f.Normal.Z), f.Vertices))
                    .ToList(),
                Color: Colors.Green
            ),
            new Cell4(
                Normal: new( 0, 1, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, 1 + cellHeight, v.Y, v.Z))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.X, 0, f.Normal.Y, f.Normal.Z), f.Vertices))
                    .ToList(),
                Color: Colors.White
            ),
            new Cell4(
                Normal: new( 0,-1, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, -1 - cellHeight, v.Y, v.X))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Z, 0, f.Normal.Y, f.Normal.X), f.Vertices))
                    .ToList(),
                Color: Colors.Yellow
            ),
            new Cell4(
                Normal: new( 0, 0, 1, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, 1 + cellHeight, v.X))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Z, f.Normal.Y, 0, f.Normal.X), f.Vertices))
                    .ToList(),
                Color: Colors.Red
            ),
            new Cell4(
                Normal: new( 0, 0,-1, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, -1 - cellHeight, v.Z))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.X, f.Normal.Y, 0, f.Normal.Z), f.Vertices))
                    .ToList(),
                Color: Colors.Orange
            ),
            new Cell4(
                Normal: new( 0, 0, 0, 1),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, v.Z, 1 + cellHeight))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.X, f.Normal.Y, f.Normal.Z, 0), f.Vertices))
                    .ToList(),
                Color: Colors.Purple
            ),
            new Cell4(
                Normal: new( 0, 0, 0,-1),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, v.X, -1 - cellHeight))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Z, f.Normal.Y, f.Normal.X, 0), f.Vertices))
                    .ToList(),
                Color: Colors.Pink
            ),
        };
    }

    public static List<Cell4> makeHyperRubiksCube(float gapWidth, float cellHeight)
    {
        var r = (1 + gapWidth) / 3;
        var grid = new List<float> { -r, 0, r };
        var cube0 = Cell3.Cube.Transform((1 - 2 * gapWidth) / 3);
        var cubes =
            from x in grid
            from y in grid
            from z in grid
            select cube0.Transform(Quaternion.Identity, new Vector3(x, y, z));

        var res = new List<Cell4>();

        foreach (var cube in cubes)
        {
            res.Add(new Cell4(
                Normal: new( 1, 0, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(1 + cellHeight, v.Z, v.Y, v.X))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(0, f.Normal.Z, f.Normal.Y, f.Normal.X), f.Vertices))
                    .ToList(),
                Color: Colors.Blue
            ));
            res.Add(new Cell4(
                Normal: new(-1, 0, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(-1 - cellHeight, v.X, v.Y, v.Z))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(0, f.Normal.X, f.Normal.Y, f.Normal.Z), f.Vertices))
                    .ToList(),
                Color: Colors.Green
            ));
            res.Add(new Cell4(
                Normal: new(0, 1, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, 1 + cellHeight, v.Y, v.Z))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.X, 0, f.Normal.Y, f.Normal.Z), f.Vertices))
                    .ToList(),
                Color: Colors.White
            ));
            res.Add(new Cell4(
                Normal: new(0, -1, 0, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, -1 - cellHeight, v.Y, v.X))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Z, 0, f.Normal.Y, f.Normal.X), f.Vertices))
                    .ToList(),
                Color: Colors.Yellow
            ));
            res.Add(new Cell4(
                Normal: new(0, 0, 1, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, 1 + cellHeight, v.X))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Z, f.Normal.Y, 0, f.Normal.X), f.Vertices))
                    .ToList(),
                Color: Colors.Red
            ));
            res.Add(new Cell4(
                Normal: new(0, 0, -1, 0),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, -1 - cellHeight, v.Z))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.X, f.Normal.Y, 0, f.Normal.Z), f.Vertices))
                    .ToList(),
                Color: Colors.Orange
            ));
            res.Add(new Cell4(
                Normal: new(0, 0, 0, 1),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, v.Z, 1 + cellHeight))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.X, f.Normal.Y, f.Normal.Z, 0), f.Vertices))
                    .ToList(),
                Color: Colors.Purple
            ));
            res.Add(new Cell4(
                Normal: new(0, 0, 0, -1),
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, v.X, -1 - cellHeight))
                    .ToList(),
                FaceIndices: cube.FaceIndices
                    .Select(f => new Face4Indices(new Vector4(f.Normal.Z, f.Normal.Y, f.Normal.X, 0), f.Vertices))
                    .ToList(),
                Color: Colors.Pink
            ));
        }

        return res;
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
        var rotation2 = CreateRotationXY((float) -Math.Atan2(axis1.W, axis1.Z));
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
        return mat * (float) (1 / Math.Sqrt(Math.Sqrt(det)));
    }
}
