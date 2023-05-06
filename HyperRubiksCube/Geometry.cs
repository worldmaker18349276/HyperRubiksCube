using Utils;
using System.Diagnostics;
using System.Numerics;

namespace Geometry;

record Face2(List<Vector2> Vertices, Color Color);

record RawFace(List<int> Vertices, Color Color);

record Cell2(List<Vector2> Vertices, List<RawFace> RawFaces)
{
    public List<Face2> Faces
    {
        get => RawFaces
            .Select(face => face is null
                            ? null
                            : new Face2(face.Vertices.Select(i => Vertices[i]).ToList(), face.Color))
            .ToList();
    }
}

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
        var vertices = face.Vertices.Select(ProjectPosition).ToList();
        var faceProj = new Face2(vertices, face.Color);
        if (ComputeNormalValue(faceProj) < 0)
            return null;
        return faceProj;
    }

    static float ComputeNormalValue(Face2 face)
    {
        if (face.Vertices.Count < 3)
            return 0;

        var v = new List<Vector2>();
        v.Add(face.Vertices[0] - face.Vertices[face.Vertices.Count - 1]);
        for (var i = 1; i < face.Vertices.Count; i++)
            v.Add(face.Vertices[i] - face.Vertices[i - 1]);
        float w = 0;
        w += DiamondVolume(v[face.Vertices.Count - 1], v[0]);
        for (var i = 1; i < face.Vertices.Count; i++)
            w += DiamondVolume(v[i - 1], v[i]);
        return w;
    }

    static float DiamondVolume(Vector2 v1, Vector2 v2)
    {
        return v1.X * v2.Y - v1.Y * v2.X;
    }

    public Cell2 ProjectCell(Cell3 cell)
    {
        if (cell is null)
            return null;
        var vertices = cell.Vertices.Select(ProjectPosition).ToList();
        var faces = cell.RawFaces
            .Select(face => ComputeNormalValue(vertices, face) >= 0
                            ? new RawFace(face.Vertices, face.Color)
                            : null)
            .ToList();

        return new Cell2(vertices, faces);
    }

    static float ComputeNormalValue(List<Vector2> vertices, RawFace face)
    {
        return ComputeNormalValue(new Face2(face.Vertices.Select(i => vertices[i]).ToList(), face.Color));
    }

    public List<Cell2> ProjectCells(List<Cell3> cells)
    {
        var cellsProj = cells
            .Where(cell => cell is not null)
            .Select(cell =>
            {
                var proj = ProjectCell(cell);
                var dis = cell.Vertices.Select(ProjectionDistance).ToList();
                var rng = (dis.Min(), dis.Max());
                return (cell, proj, dis, rng);
            })
            .OrderByDescending(e => e.Item4)
            .ToList();

        // return cellsProj.Select(elem => elem.Item2).ToList();
        return Algorithm.TopoSort(cellsProj, Shadow).Select(elem => elem.Item2).ToList();
    }

    int Shadow(
        (Cell3, Cell2, List<float>, (float, float)) elem0,
        (Cell3, Cell2, List<float>, (float, float)) elem1
    ) {
        var (cell0, proj0, dis0, rng0) = elem0;
        var (cell1, proj1, dis1, rng1) = elem1;

        if (rng0.Item2 < rng1.Item1 || rng1.Item2 < rng0.Item1)
            return 0;

        foreach (var b in Enumerable.Range(0, cell1.RawFaces.Count))
        {
            if (proj1.RawFaces[b] is null)
                continue;

            var faceb = new Face3(
                cell1.RawFaces[b].Vertices.Select(i => cell1.Vertices[i]).ToList(),
                cell1.RawFaces[b].Color
                );
            var facebProj = new Face2(
                proj1.RawFaces[b].Vertices.Select(i => proj1.Vertices[i]).ToList(),
                proj1.RawFaces[b].Color
                );

            var xs = facebProj.Vertices.Select(v => v.X).ToList();
            var ys = facebProj.Vertices.Select(v => v.Y).ToList();
            var xrng = (xs.Min(), xs.Max());
            var yrng = (ys.Min(), ys.Max());

            foreach (var a in Enumerable.Range(0, cell0.Vertices.Count))
            {
                var disa = dis0[a];
                var pointa = cell0.Vertices[a];
                var pointaProj = proj0.Vertices[a];

                if (pointaProj.X < xrng.Item1 || pointaProj.X > xrng.Item2)
                    continue;
                if (pointaProj.Y < yrng.Item1 || pointaProj.Y > yrng.Item2)
                    continue;

                var pointab = ProjectPointToFace(
                    pointa,
                    pointaProj,
                    faceb,
                    facebProj
                );
                if (pointab is null)
                    continue;
                return disa < ProjectionDistance(pointab.Value) ? 1 : -1;
            }
        }

        foreach (var a in Enumerable.Range(0, cell0.RawFaces.Count))
        {
            if (proj0.RawFaces[a] is null)
                continue;

            var facea = new Face3(
                cell0.RawFaces[a].Vertices.Select(i => cell0.Vertices[i]).ToList(),
                cell0.RawFaces[a].Color
                );
            var faceaProj = new Face2(
                proj0.RawFaces[a].Vertices.Select(i => proj0.Vertices[i]).ToList(),
                proj0.RawFaces[a].Color
                );

            var xs = faceaProj.Vertices.Select(v => v.X).ToList();
            var ys = faceaProj.Vertices.Select(v => v.Y).ToList();
            var xrng = (xs.Min(), xs.Max());
            var yrng = (ys.Min(), ys.Max());

            foreach (var b in Enumerable.Range(0, cell1.Vertices.Count))
            {
                var disb = dis1[b];
                var pointb = cell1.Vertices[b];
                var pointbProj = proj1.Vertices[b];

                if (pointbProj.X < xrng.Item1 || pointbProj.X > xrng.Item2)
                    continue;
                if (pointbProj.Y < yrng.Item1 || pointbProj.Y > yrng.Item2)
                    continue;

                var pointba = ProjectPointToFace(
                    pointb,
                    pointbProj,
                    facea,
                    faceaProj
                );
                if (pointba is null)
                    continue;
                return disb < ProjectionDistance(pointba.Value) ? -1 : 1;
            }
        }

        return 0;
    }

    Vector3? ProjectPointToFace(Vector3 point, Vector2 pointProj, Face3 face, Face2 faceProj)
    {
        var planeNullable = face.ComputePlane();
        if (planeNullable is null)
            return null;
        var plane = planeNullable.Value;

        for (var i = 0; i < faceProj.Vertices.Count; i++)
        {
            var iprev = i - 1;
            iprev = iprev < 0 ? faceProj.Vertices.Count - 1 : iprev;
            var v1 = faceProj.Vertices[iprev];
            var v2 = faceProj.Vertices[i];
            var v12 = Vector2.Normalize(v2 - v1);
            var n = new Vector2(-v12.Y, v12.X);
            var d = (Vector2.Dot(v1, n) + Vector2.Dot(v2, n)) / 2;
            if (d > Vector2.Dot(pointProj, n))
                return null;
        }

        var dir = float.IsInfinity(FocalLength)
                ? Looking
                : Vector3.Normalize(point - FocalPoint);
        var dis = plane.D - Vector3.Dot(plane.Normal, point);
        var ratio = dis / Vector3.Dot(plane.Normal, dir);
        var pointOnPlane = point + dir * ratio;
        return pointOnPlane;
    }
}

record Face3(List<Vector3> Vertices, Color Color)
{
    public Plane? ComputePlane()
    {
        if (Vertices.Count < 3)
            return null;

        var v = new List<Vector3>();
        v.Add(Vertices[0] - Vertices[Vertices.Count - 1]);
        for (var i = 1; i < Vertices.Count; i++)
            v.Add(Vertices[i] - Vertices[i - 1]);
        var w = new Vector3(0, 0, 0);
        w += Vector3.Cross(v[Vertices.Count - 1], v[0]);
        for (var i = 1; i < Vertices.Count; i++)
            w += Vector3.Cross(v[i - 1], v[i]);
        w = Vector3.Normalize(w);

        float d = 0;
        foreach (var vertex in Vertices)
            d += Vector3.Dot(w, vertex);
        d = d / Vertices.Count;

        return new Plane(w, d);
    }
}

record RawCorner(int Vertex, List<(int, int)> Angles);

record Cell3(List<Vector3> Vertices, List<RawFace> RawFaces, List<RawCorner> RawCorners)
{
    public List<Face3> Faces
    {
        get
        {
            return RawFaces.Select(
                face => face is null
                        ? null
                        : new Face3(
                            Vertices: face.Vertices.Select(i => Vertices[i]).ToList(),
                            Color: face.Color
                        )
            ).ToList();
        }
    }

    public Cell3 Transform(float scale)
    {
        return new Cell3(
            Vertices.Select(v => v * scale).ToList(),
            RawFaces,
            RawCorners
        );
    }

    public Cell3 Transform(Quaternion rotation)
    {
        return new Cell3(
            Vertices.Select(v => Vector3.Transform(v, rotation)).ToList(),
            RawFaces,
            RawCorners
        );
    }

    public Cell3 Transform(Quaternion rotation, Vector3 translation)
    {
        return new Cell3(
            Vertices
            .Select(v => Vector3.Transform(v, rotation) + translation)
            .ToList(),
            RawFaces,
            RawCorners
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
        if (cell is null)
            return null;
        var vertices = cell.Vertices.Select(ProjectPosition).ToList();
        var cellProj = new Cell3(vertices, cell.RawFaces, cell.RawCorners);
        if (ComputeNormalValue(cellProj) < 0)
            return null;
        return cellProj;
    }

    static float ComputeNormalValue(Cell3 cell)
    {
        float w = 0;
        foreach (var corner in cell.RawCorners)
        {
            var vertex = cell.Vertices[corner.Vertex];
            var edges = corner.Angles
                .Select(ij =>
                {
                    var face = cell.RawFaces[ij.Item1];
                    var j = ij.Item2 + 1;
                    j = j < face.Vertices.Count ? j : 0;
                    var vertexNext = cell.Vertices[face.Vertices[j]];
                    return vertexNext - vertex;
                })
                .ToList();
            w += DiamondVolume(edges);
        }
        return w;
    }

    static float DiamondVolume(List<Vector3> edges)
    {
        if (edges.Count < 3)
            return 0;

        float vol = 0;
        var v00 = edges[edges.Count - 2];
        var v01 = edges[edges.Count - 1];
        var v02 = edges[0];
        vol += Vector3.Dot(Vector3.Cross(v00, v01), v02);

        var v10 = edges[edges.Count - 1];
        var v11 = edges[0];
        var v12 = edges[1];
        vol += Vector3.Dot(Vector3.Cross(v10, v11), v12);

        for (var i = 2; i < edges.Count; i++)
        {
            var v0 = edges[i - 2];
            var v1 = edges[i - 1];
            var v2 = edges[i];
            vol += Vector3.Dot(Vector3.Cross(v0, v1), v2);
        }
        return vol;
    }
}

record Cell4(List<Vector4> Vertices, List<RawFace> RawFaces, List<RawCorner> RawCorners, Color Color)
{
    public Cell4 Transform(float scale)
    {
        return new Cell4(
            Vertices.Select(v => v * scale).ToList(),
            RawFaces,
            RawCorners,
            Color
        );
    }

    public Cell4 Transform(Matrix4x4 rotation)
    {
        return new Cell4(
            Vertices.Select(v => Vector4.Transform(v, rotation)).ToList(),
            RawFaces,
            RawCorners,
            Color
        );
    }

    public Cell4 Transform(Matrix4x4 rotation, Vector4 translation)
    {
        return new Cell4(
            Vertices
            .Select(v => Vector4.Transform(v, rotation) + translation)
            .ToList(),
            RawFaces,
            RawCorners,
            Color
        );
    }
}

static class HyperCubeBuilder
{
    public static Cell3 MakeCube()
    {
        var cubeVertices = new List<Vector3>();
        var signs = new float[] { +1, -1 };
        foreach (var i in signs)
            foreach (var j in signs)
                foreach (var k in signs)
                    cubeVertices.Add(new(i, j, k));
        var cubeRawFaces = new List<(Vector3, RawFace)>()
        {
            (new Vector3( 1, 0, 0), new RawFace(new List<int> {
                4*0+2*0+1*0,
                4*0+2*1+1*0,
                4*0+2*1+1*1,
                4*0+2*0+1*1,
            }, Colors.Blue)),
            (new Vector3(-1, 0, 0), new RawFace(new List<int> {
                4*1+2*0+1*0,
                4*1+2*0+1*1,
                4*1+2*1+1*1,
                4*1+2*1+1*0,
            }, Colors.Green)),
            (new Vector3( 0, 1, 0), new RawFace(new List<int> {
                2*0+1*0+4*0,
                2*0+1*1+4*0,
                2*0+1*1+4*1,
                2*0+1*0+4*1,
            }, Colors.White)),
            (new Vector3( 0,-1, 0), new RawFace(new List<int> {
                2*1+1*0+4*0,
                2*1+1*0+4*1,
                2*1+1*1+4*1,
                2*1+1*1+4*0,
            }, Colors.Yellow)),
            (new Vector3( 0, 0, 1), new RawFace(new List<int> {
                1*0+4*0+2*0,
                1*0+4*1+2*0,
                1*0+4*1+2*1,
                1*0+4*0+2*1,
            }, Colors.Red)),
            (new Vector3( 0, 0,-1), new RawFace(new List<int> {
                1*1+4*0+2*0,
                1*1+4*0+2*1,
                1*1+4*1+2*1,
                1*1+4*1+2*0,
            }, Colors.Orange)),
        };

        var cubeRawCorners =
            Enumerable.Range(0, cubeVertices.Count)
            .Select(k =>
            {
                var vertex = cubeVertices[k];
                var corner = Enumerable.Range(0, cubeRawFaces.Count)
                    .Where(i =>
                    {
                        var (normal, f) = cubeRawFaces[i];
                        return normal.X == vertex.X
                            || normal.Y == vertex.Y
                            || normal.Z == vertex.Z;
                    })
                    .Select(i =>
                    {
                        var j = cubeRawFaces[i].Item2.Vertices.FindIndex(j => k == j);
                        Debug.Assert(j != -1);
                        return (i, j);
                    })
                    .ToList();

                Debug.Assert(corner.Count == 3);
                var normals = corner
                    .Select(ij => cubeRawFaces[ij.Item1].Item1)
                    .ToList();
                var vol = Vector3.Dot(Vector3.Cross(normals[0], normals[1]), normals[2]);
                if (vol > 0)
                    (corner[1], corner[2]) = (corner[2], corner[1]);
                return new RawCorner(k, corner);
            })
            .ToList();

        return new Cell3(cubeVertices, cubeRawFaces.Select(e => e.Item2).ToList(), cubeRawCorners);
    }

    public static List<Cell4> MakeHyperCube(float cellHeight)
    {
        var cube = MakeCube();

        return new List<Cell4> {
            new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(1 + cellHeight, v.Z, v.Y, v.X))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Blue))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Blue
            ),
            new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(- 1 - cellHeight, v.X, v.Y, v.Z))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Green))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Green
            ),
            new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, 1 + cellHeight, v.Y, v.Z))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.White))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.White
            ),
            new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, -1 - cellHeight, v.Y, v.X))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Yellow))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Yellow
            ),
            new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, 1 + cellHeight, v.X))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Red))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Red
            ),
            new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, -1 - cellHeight, v.Z))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Orange))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Orange
            ),
            new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, v.Z, 1 + cellHeight))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Purple))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Purple
            ),
            new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, v.X, -1 - cellHeight))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Pink))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Pink
            ),
        };
    }

    public static List<Cell4> MakeHyperRubiksCube(float gapWidth, float cellHeight)
    {
        var r = (1 + gapWidth) / 3;
        var grid = new List<float> { -r, 0, r };
        var cube0 = MakeCube().Transform((1 - 2 * gapWidth) / 3);
        var cubes =
            from x in grid
            from y in grid
            from z in grid
            select cube0.Transform(Quaternion.Identity, new Vector3(x, y, z));

        var res = new List<Cell4>();

        foreach (var cube in cubes)
        {
            res.Add(new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(1 + cellHeight, v.Z, v.Y, v.X))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Blue))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Blue
            ));
            res.Add(new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(-1 - cellHeight, v.X, v.Y, v.Z))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Green))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Green
            ));
            res.Add(new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, 1 + cellHeight, v.Y, v.Z))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.White))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.White
            ));
            res.Add(new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, -1 - cellHeight, v.Y, v.X))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Yellow))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Yellow
            ));
            res.Add(new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, 1 + cellHeight, v.X))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Red))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Red
            ));
            res.Add(new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, -1 - cellHeight, v.Z))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Orange))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Orange
            ));
            res.Add(new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.X, v.Y, v.Z, 1 + cellHeight))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Purple))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Purple
            ));
            res.Add(new Cell4(
                Vertices: cube.Vertices
                    .Select(v => new Vector4(v.Z, v.Y, v.X, -1 - cellHeight))
                    .ToList(),
                RawFaces: cube.RawFaces
                    .Select(f => new RawFace(f.Vertices, Colors.Pink))
                    .ToList(),
                RawCorners: cube.RawCorners,
                Color: Colors.Pink
            ));
        }

        return res;
    }
}
