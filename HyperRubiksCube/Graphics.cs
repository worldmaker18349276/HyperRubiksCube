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

        var camera = new Camera3(
            orientation: Quaternion.Identity,
            focalLength: float.PositiveInfinity,
            screenDistance: 2
        );

        var face1 = new Face3(
            new Vector3(0, 0, 1),
            new List<Vector3> {
                new( 1,  1, 0),
                new(-1,  1, 0),
                new(-1, -1, 0),
                new( 1, -1, 0)
            },
            Colors.SlateBlue
        );
        face1.Transform(Quaternion.CreateFromAxisAngle(new(1, 1, 0), float.Pi/5));

        screen.DrawFace(camera.ProjectFace(face1));
    }
}

record Screen(ICanvas Canvas, PointF Center, float Ratio)
{
    public PointF Convert(Vector2 point)
    {
        return new PointF(Center.X + point.X * Ratio, Center.Y + point.Y * Ratio);
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
}
