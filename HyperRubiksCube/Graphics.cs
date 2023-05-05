using Geometry;
using System.Numerics;

namespace Graphics;


public class HyperCubeScene : IDrawable
{
    Camera3 Camera;
    Camera4 HyperCamera;
    List<Cell4> Cells;

    Matrix4x4 Step;

    public HyperCubeScene()
    {
        Camera = new Camera3(
            orientation: Quaternion.CreateFromYawPitchRoll(
                yaw: float.Pi * 1 / 6,
                pitch: -float.Pi * 1 / 5,
                roll: 0
            ),
            focalLength: 10,
            screenDistance: 2
        );

        var orientation4 =
            Matrix4x4Extension.CreateRotationXY(float.Pi / 3)
            * Matrix4x4.CreateFromYawPitchRoll(
                yaw: float.Pi * 1 / 6,
                pitch: -float.Pi * 1 / 5,
                roll: 0
            );
        HyperCamera = new Camera4(
            orientation: orientation4,
            focalLength: -10,
            screenDistance: 2
        );

        Cells = HyperCube.makeHyperCube(0.3f);

        Step =
            Matrix4x4Extension.CreateRotationXY(float.Pi / 120)
            * Matrix4x4.CreateFromYawPitchRoll(
                yaw: float.Pi / 100,
                pitch: float.Pi / 160,
                roll: float.Pi / 200
            );
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
        var cubes = Cells
            .Select(HyperCamera.ProjectCell)
            .Where(cell => cell != null)
            .ToList();
        screen.DrawFaces(Camera.ProjectCells(cubes));
    }

    public void Spin(Vector2 diff)
    {
        var scale = diff.Length();
        var axis = Vector3.Normalize(new(-diff.Y, diff.X, 0));
        var rotation = Quaternion.CreateFromAxisAngle(axis, scale);
        Camera.Orientation = Quaternion.Normalize(Camera.Orientation * rotation);
    }

    public void Advance()
    {
        HyperCamera.Orientation = (Step * HyperCamera.Orientation).Normalize();
    }
}

class PanGestureHandler
{
    double X = 0;
    double Y = 0;
    readonly double Ratio;
    readonly double Tolerance = 0.1;
    readonly HyperCubeScene Scene;

    public PanGestureHandler(HyperCubeScene scene, double ratio)
    {
        Scene = scene;
        Ratio = ratio;
    }

    public void OnPanUpdated(object sender, PanUpdatedEventArgs eventArgs)
    {
        switch (eventArgs.StatusType)
        {
            case GestureStatus.Started:
                X = eventArgs.TotalX;
                Y = eventArgs.TotalY;
                break;
            case GestureStatus.Running:
                var x = eventArgs.TotalX;
                var y = eventArgs.TotalY;
                var diff = new Vector2((float)((X - x) / Ratio), (float)((y - Y) / Ratio));
                if (diff.Length() > Tolerance)
                {
                    Scene.Spin(diff);
                    X = x;
                    Y = y;
                }
                break;
        }
    }
}

public partial class HyperCubeView : GraphicsView
{
    public HyperCubeView() {
        var scene = new HyperCubeScene();
        Drawable = scene;

        var panGesture = new PanGestureRecognizer();
        var handler = new PanGestureHandler(scene, 30);
        panGesture.PanUpdated += handler.OnPanUpdated;
        GestureRecognizers.Add(panGesture);

        IDispatcherTimer timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.Tick += (s, e) => {
            scene.Advance();
            Invalidate();
        };
        timer.Start();
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
