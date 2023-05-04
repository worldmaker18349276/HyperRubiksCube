using Geometry;
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace Graphics;

public class GraphicsDrawable : IDrawable
{
    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.DarkGray;
        canvas.FillRectangle(dirtyRect);

        var screen = new Screen(
            Canvas: canvas,
            Center: dirtyRect.Center,
            Ratio: 50
        );

        var yaw = float.Pi * 1 / 6;
        var pitch = -float.Pi * 1 / 5;
        var camera = new Camera3(
            Orientation: Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0),
            FocalLength: float.PositiveInfinity,
            ScreenDistance: 2
        );

        screen.DrawFaces(camera.ProjectFaces(Polyhedron.Cube));
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
