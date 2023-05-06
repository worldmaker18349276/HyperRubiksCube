using Microsoft.Maui.Controls;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace Geometry;

record Face2(List<Vector2> Vertices, Color Color);

record Face2Indices(bool IsVisible, List<Index> Vertices, Color Color);

record Cell2(List<Vector2> Vertices, List<Face2Indices> FaceIndices)
{
    public List<Face2> VisibleFaces
    {
        get => FaceIndices
            .Where(face => face.IsVisible)
            .Select(face => new Face2(face.Vertices.Select(i => Vertices[i]).ToList(), face.Color))
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
        // if (Vector3.Dot(face.Normal, Looking) > 0)
        //     return null;

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
        var vertices = cell.Vertices.Select(ProjectPosition).ToList();
        var faces = cell.FaceIndices
            .Select(face => new Face2Indices(
                ComputeNormalValue(vertices, face) >= 0, face.Vertices, face.Color))
            .ToList();

        return new Cell2(vertices, faces);
    }

    static float ComputeNormalValue(List<Vector2> vertices, Face3Indices face)
    {
        return ComputeNormalValue(new Face2(face.Vertices.Select(i => vertices[i]).ToList(), face.Color));
    }

    public List<Cell2> ProjectCells(List<Cell3> cells)
    {
        var cellsProj = cells
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
        return TopoSort(cellsProj, Shadow).Select(elem => elem.Item2).ToList();
    }

    int Shadow(
        (Cell3, Cell2, List<float>, (float, float)) elem0,
        (Cell3, Cell2, List<float>, (float, float)) elem1
    ) {
        var (cell0, proj0, dis0, rng0) = elem0;
        var (cell1, proj1, dis1, rng1) = elem1;

        if (rng0.Item2 < rng1.Item1 || rng1.Item2 < rng0.Item1)
            return 0;

        foreach (var b in Enumerable.Range(0, cell1.FaceIndices.Count))
        {
            if (!proj1.FaceIndices[b].IsVisible)
                continue;
            var faceb = new Face3(
                cell1.FaceIndices[b].Normal,
                cell1.FaceIndices[b].Vertices.Select(i => cell1.Vertices[i]).ToList(),
                cell1.FaceIndices[b].Color
                );
            var facebProj = new Face2(
                proj1.FaceIndices[b].Vertices.Select(i => proj1.Vertices[i]).ToList(),
                proj1.FaceIndices[b].Color
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

                var disb = ProjectToFace(
                    pointa,
                    pointaProj,
                    faceb,
                    facebProj
                );
                if (disb is null)
                    continue;
                return disa < disb.Value ? 1 : -1;
            }
        }

        foreach (var a in Enumerable.Range(0, cell0.FaceIndices.Count))
        {
            if (!proj0.FaceIndices[a].IsVisible)
                continue;
            var facea = new Face3(
                cell0.FaceIndices[a].Normal,
                cell0.FaceIndices[a].Vertices.Select(i => cell0.Vertices[i]).ToList(),
                cell0.FaceIndices[a].Color
                );
            var faceaProj = new Face2(
                proj0.FaceIndices[a].Vertices.Select(i => proj0.Vertices[i]).ToList(),
                proj0.FaceIndices[a].Color
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

                var disa = ProjectToFace(
                    pointb,
                    pointbProj,
                    facea,
                    faceaProj
                );
                if (disa is null)
                    continue;
                return disb < disa.Value ? -1 : 1;
            }
        }

        return 0;
    }

    float? ProjectToFace(Vector3 point, Vector2 pointProj, Face3 face, Face2 faceProj)
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
        return ProjectionDistance(pointOnPlane);
    }

    List<T> TopoSort<T>(List<T> elems, Func<T, T, int> hasEdge)
    {
        var edges = new HashSet<(int, int)>();
        foreach (var i in Enumerable.Range(0, elems.Count - 1))
            foreach (var j in Enumerable.Range(i + 1, elems.Count - i - 1))
            {
                var d = hasEdge(elems[i], elems[j]);
                if (d < 0)
                    edges.Add((i, j));
                else if (d > 0)
                    edges.Add((j, i));
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

record Face3(Vector3 Normal, List<Vector3> Vertices, Color Color)
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

record Corner3Indices(Index Vertex, List<(Index, Index)> FaceVertices);

record Cell3(List<Vector3> Vertices, List<Face3Indices> FaceIndices, List<Corner3Indices> CornerIndices)
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

        var cornerIndices =
            Enumerable.Range(0, cubeVertices.Count)
            .Select(k =>
            {
                var vertex = cubeVertices[k];
                var corner = Enumerable.Range(0, cubeFaceIndices.Count)
                    .Where(i =>
                    {
                        var f = cubeFaceIndices[i];
                        return f.Normal.X == vertex.X
                            || f.Normal.Y == vertex.Y
                            || f.Normal.Z == vertex.Z;
                    })
                    .Select(i =>
                    {
                        var j = cubeFaceIndices[i].Vertices.FindIndex(j => k == j.Value);
                        Debug.Assert(j != -1);
                        return ((Index) i, (Index) j);
                    })
                    .ToList();

                Debug.Assert(corner.Count == 3);
                var normals = corner
                    .Select(ij => cubeFaceIndices[ij.Item1].Normal)
                    .ToList();
                var vol = Vector3.Dot(Vector3.Cross(normals[0], normals[1]), normals[2]);
                if (vol > 0)
                    (corner[1], corner[2]) = (corner[2], corner[1]);
                return new Corner3Indices(k, corner);
            })
            .ToList();

        Cube = new Cell3(cubeVertices, cubeFaceIndices, cornerIndices);
    }

    public Cell3 Transform(float scale)
    {
        return new Cell3(
            Vertices.Select(v => v * scale).ToList(),
            FaceIndices,
            CornerIndices
        );
    }

    public Cell3 Transform(Quaternion rotation)
    {
        return new Cell3(
            Vertices.Select(v => Vector3.Transform(v, rotation)).ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList(),
            CornerIndices
        );
    }

    public Cell3 Transform(Quaternion rotation, Vector3 translation)
    {
        return new Cell3(
            Vertices
            .Select(v => Vector3.Transform(v, rotation) + translation)
            .ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList(),
            CornerIndices
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
        // if (Vector4.Dot(cell.Normal, Looking) > 0)
        //     return null;

        var vertices = cell.Vertices.Select(ProjectPosition).ToList();
        var faceIndices = cell.FaceIndices
            .Select(f => new Face3Indices(ProjectTensor((cell.Normal, f.Normal)), f.Vertices, cell.Color))
            .ToList();
        var cellProj = new Cell3(vertices, faceIndices, cell.CornerIndices);
        if (ComputeNormalValue(cellProj) < 0)
            return null;
        return cellProj;
    }

    static float ComputeNormalValue(Cell3 cell)
    {
        float w = 0;
        foreach (var corner in cell.CornerIndices)
        {
            var vertex = cell.Vertices[corner.Vertex];
            var edges = corner.FaceVertices
                .Select(ij =>
                {
                    var face = cell.FaceIndices[ij.Item1];
                    var j = ij.Item2.Value + 1;
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

record Face4Indices(Vector4 Normal, List<Index> Vertices)
{
    public Face4Indices Transform(Matrix4x4 rotation)
    {
        return new Face4Indices(Vector4.Transform(Normal, rotation), Vertices);
    }

}

record Cell4(Vector4 Normal, List<Vector4> Vertices, List<Face4Indices> FaceIndices, List<Corner3Indices> CornerIndices, Color Color)
{
    public Cell4 Transform(float scale)
    {
        return new Cell4(
            Normal,
            Vertices.Select(v => v * scale).ToList(),
            FaceIndices,
            CornerIndices,
            Color
        );
    }

    public Cell4 Transform(Matrix4x4 rotation)
    {
        return new Cell4(
            Vector4.Transform(Normal, rotation),
            Vertices.Select(v => Vector4.Transform(v, rotation)).ToList(),
            FaceIndices.Select(f => f.Transform(rotation)).ToList(),
            CornerIndices,
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
            CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
                CornerIndices: cube.CornerIndices,
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
