using Geometry;
using System.Numerics;

namespace Graphics;


public class HyperCubeScene
{
    internal Camera3 Camera { get; set; }
    internal Camera4 HyperCamera { get; set; }
    internal List<Cell4> Cells { get; set; }

    public HyperCubeScene()
    {
        Camera = new Camera3(
            Orientation: Quaternion.CreateFromYawPitchRoll(
                yaw: float.Pi * 1 / 6,
                pitch: -float.Pi * 1 / 5,
                roll: 0
            ),
            FocalLength: 10,
            ScreenDistance: 2
        );

        var orientation4 =
            Matrix4x4Extension.CreateRotationXY(float.Pi / 3)
            * Matrix4x4.CreateFromYawPitchRoll(
                yaw: float.Pi * 1 / 6,
                pitch: -float.Pi * 1 / 5,
                roll: 0
            );
        HyperCamera = new Camera4(
            Orientation: orientation4,
            FocalLength: -10,
            ScreenDistance: 2
        );

        Cells = HyperCube.makeHyperCube(0.3f);
    }

}

public partial class HyperCubeView : GraphicsView
{
    public HyperCubeView() {
        var scene = new HyperCubeScene();
        var drawable = new HyperCubeDrawable(scene);
        Drawable = drawable;
    }
}

public class HyperCubeDrawable : IDrawable
{
    HyperCubeScene Scene { get; set; }

    public HyperCubeDrawable(HyperCubeScene scene)
    {
        Scene = scene;
    }

    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.DarkGray;
        canvas.FillRectangle(dirtyRect);

        var screen = new Screen(
            Canvas: canvas,
            Center: dirtyRect.Center,
            Ratio: 50
        );
        var cubes = Scene.Cells
            .Select(Scene.HyperCamera.ProjectCell)
            .Where(cell => cell != null)
            .ToList();
        screen.DrawFaces(Scene.Camera.ProjectCells(cubes));
    }
}

record Screen(ICanvas Canvas, PointF Center, float Ratio)
{
    public PointF Convert(Vector2 point)
    {
        return new PointF(Center.X + point.X * Ratio, Center.Y - point.Y * Ratio);
    }

    public void DrawFace(Face2 face)
    {
        if (face == null)
            return;

        var path = new PathF();
        if (face.Vertices.Any())
            path.MoveTo(Convert(face.Vertices.First()));
        foreach (var curr in face.Vertices.Skip(1))
            path.LineTo(Convert(curr));
        Canvas.FillColor = face.Color;
        Canvas.FillPath(path);
    }

    public void DrawFaces(List<Face2> faces)
    {
        foreach (var face in faces)
        {
            DrawFace(face);
        }
    }
}
