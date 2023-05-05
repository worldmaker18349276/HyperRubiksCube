using Geometry;
using System.Numerics;

namespace Graphics;


public class HyperCubeScene : IDrawable
{
    Camera3 Camera;
    Camera4 HyperCamera;
    List<Cell4> Cells;

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
            Matrix4x4.CreateFromYawPitchRoll(
                yaw: float.Pi * 1 / 6,
                pitch: -float.Pi * 1 / 5,
                roll: 0
            ) * Matrix4x4Extension.CreateRotationXY(float.Pi / 3);
        HyperCamera = new Camera4(
            orientation: orientation4,
            focalLength: -10,
            screenDistance: 2
        );

        Cells = HyperCube.makeHyperCube(0.3f);
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
        // axis = (0,0,-1) x (diff.X, diff.Y, 0)
        var axis = Vector3.Normalize(new(-diff.Y, diff.X, 0));
        var rotation = Quaternion.CreateFromAxisAngle(axis, scale);
        Camera.Orientation = Quaternion.Normalize(Camera.Orientation * rotation);
    }

    public void Gyrospin(Vector3 diff)
    {
        var scale = diff.Length();
        diff = Vector3.Transform(diff, Camera.Orientation);

        // find two orthogonal vectors (axis1, axis2) to diff

        // axis1 = (0,0,-1) x diff or (0,1,0) x diff
        var axis11 = Vector3.Cross(new(0, 0, -1), diff);
        var axis12 = Vector3.Cross(new(0, 1, 0), diff);
        Vector3 axis1;
        if (axis11.Length() > axis12.Length())
            axis1 = axis11;
        else
            axis1 = axis12;
        axis1 = Vector3.Normalize(axis1);

        // axis2 = diff x axis1
        var axis2 = Vector3.Normalize(Vector3.Cross(diff, axis1));

        var axis = (
            new Vector4(axis1.X, axis1.Y, axis1.Z, 0),
            new Vector4(axis2.X, axis2.Y, axis2.Z, 0)
        );
        var rotation = Matrix4x4Extension.Create4DRotationFromAxisAngle(axis, scale);

        HyperCamera.Orientation = (rotation * HyperCamera.Orientation).Normalize();
    }
}

class GestureHandler
{
    enum ControlMode
    {
        SpinMode,
        GyrospinMode
    }

    double X = 0;
    double Y = 0;
    double Scale = 0;
    ControlMode Mode = ControlMode.SpinMode;
    readonly double Ratio;
    readonly double ZoomRatio;
    readonly double Tolerance = 0.1;
    readonly HyperCubeScene Scene;

    public GestureHandler(HyperCubeScene scene, double ratio, double zoomRatio)
    {
        Scene = scene;
        Ratio = ratio;
        ZoomRatio = zoomRatio;
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
                if (Mode == ControlMode.GyrospinMode)
                {
                    var diff = new Vector3((float)((X - x) / Ratio), (float)((y - Y) / Ratio), 0);
                    if (diff.Length() > Tolerance)
                    {
                        Scene.Gyrospin(diff);
                        X = x;
                        Y = y;
                    }
                }
                else
                {
                    var diff = new Vector2((float)((X - x) / Ratio), (float)((y - Y) / Ratio));
                    if (diff.Length() > Tolerance)
                    {
                        Scene.Spin(diff);
                        X = x;
                        Y = y;
                    }
                }
                break;
        }
    }

    public void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs eventArgs)
    {
        if (Mode != ControlMode.GyrospinMode)
            return;

        switch (eventArgs.Status)
        {
            case GestureStatus.Started:
                Scale = eventArgs.Scale;
                break;
            case GestureStatus.Running:
                var currentScale = eventArgs.Scale;
                var diff = new Vector3(0, 0, (float) ((currentScale - Scale) / ZoomRatio));
                if (diff.Length() > Tolerance)
                {
                    Scene.Gyrospin(diff);
                    Scale = currentScale;
                }
                break;
        }
    }

    public void OnTappedSecondary(object sender, TappedEventArgs eventArgs)
    {
        Mode = Mode == ControlMode.SpinMode ? ControlMode.GyrospinMode : ControlMode.SpinMode;
    }
}

public partial class HyperCubeView : GraphicsView
{
    public HyperCubeView() {
        var scene = new HyperCubeScene();
        Drawable = scene;

        var handler = new GestureHandler(scene, 80, 20);
        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += handler.OnPanUpdated;
        GestureRecognizers.Add(panGesture);
        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += handler.OnPinchUpdated;
        GestureRecognizers.Add(pinchGesture);
        var tapsecGestureRecognizer = new TapGestureRecognizer
        {
            Buttons = ButtonsMask.Secondary
        };
        tapsecGestureRecognizer.Tapped += handler.OnTappedSecondary;
        GestureRecognizers.Add(tapsecGestureRecognizer);

        IDispatcherTimer timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.Tick += (s, e) => {
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
