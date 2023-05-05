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
            focalLength: 5,
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
            focalLength: -3,
            screenDistance: 2
        );

        Cells = HyperCube.makeHyperRubiksCube(0.3f, 0.1f);
    }

    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.DarkGray;
        canvas.FillRectangle(dirtyRect);

        var screen = new Screen(
            Canvas: canvas,
            Center: dirtyRect.Center,
            Ratio: 150
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

public partial class HyperCubeView : GraphicsView
{
    enum ControlMode
    {
        SpinMode,
        GyrospinMode
    }

    new double X = 0;
    new double Y = 0;
    new double Scale = 0;
    ControlMode Mode = ControlMode.GyrospinMode;
    readonly double DragRatio = 80;
    readonly double ZoomRatio = 20;
    readonly double MotionMinValue = 0.02;
    readonly HyperCubeScene Scene;

    public HyperCubeView()
    {
        Scene = new HyperCubeScene();
        Drawable = Scene;

        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += OnPanUpdated;
        GestureRecognizers.Add(panGesture);
        var pinchGesture = new PinchGestureRecognizer();
        pinchGesture.PinchUpdated += OnPinchUpdated;
        GestureRecognizers.Add(pinchGesture);
        var tapsecGestureRecognizer = new TapGestureRecognizer
        {
            Buttons = ButtonsMask.Secondary
        };
        tapsecGestureRecognizer.Tapped += OnTappedSecondary;
        GestureRecognizers.Add(tapsecGestureRecognizer);
    }

    void OnPanUpdated(object sender, PanUpdatedEventArgs eventArgs)
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
                    var diff = new Vector3((float)((X - x) / DragRatio), (float)((y - Y) / DragRatio), 0);
                    if (diff.Length() > MotionMinValue)
                    {
                        Scene.Gyrospin(diff);
                        X = x;
                        Y = y;
                        Invalidate();
                    }
                }
                else
                {
                    var diff = new Vector2((float)((X - x) / DragRatio), (float)((y - Y) / DragRatio));
                    if (diff.Length() > MotionMinValue)
                    {
                        Scene.Spin(diff);
                        X = x;
                        Y = y;
                        Invalidate();
                    }
                }
                break;
        }
    }

    void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs eventArgs)
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
                var diff = new Vector3(0, 0, (float)((currentScale - Scale) / ZoomRatio));
                if (diff.Length() > MotionMinValue)
                {
                    Scene.Gyrospin(diff);
                    Scale = currentScale;
                    Invalidate();
                }
                break;
        }
    }

    void OnTappedSecondary(object sender, TappedEventArgs eventArgs)
    {
        Mode = Mode == ControlMode.SpinMode ? ControlMode.GyrospinMode : ControlMode.SpinMode;
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
        path.LineTo(Convert(face.Vertices.First()));
        Canvas.FillColor = face.Color;
        Canvas.FillPath(path);
        Canvas.StrokeColor = Colors.Black;
        Canvas.StrokeSize = 1;
        Canvas.DrawPath(path);
    }

    public void DrawFaces(List<Face2> faces)
    {
        foreach (var face in faces)
        {
            DrawFace(face);
        }
    }
}
