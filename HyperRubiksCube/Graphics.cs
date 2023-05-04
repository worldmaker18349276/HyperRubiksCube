using Geometry;
using System.Numerics;

namespace Graphics;

public class GraphicsDrawable : IDrawable
{
    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = Colors.Blue;
        canvas.FontSize = 18;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FillColor = Colors.DarkGray;
        canvas.FillRectangle(dirtyRect);

        var coordinate = new Coordinate(
            center: dirtyRect.Center,
            ratio: 50
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
            }
        );
        face1.Transform(Quaternion.CreateFromAxisAngle(new(1, 1, 0), float.Pi/5));

        var rect1 = camera.ProjectFace(face1);
        if (rect1 != null)
        {
            var reg = new PathF();
            if (rect1.Vertices.Any())
                reg.MoveTo(coordinate.Convert(rect1.Vertices.First()));
            foreach (var curr in rect1.Vertices.Skip(1))
                reg.LineTo(coordinate.Convert(curr));
            canvas.FillColor = Colors.SlateBlue;
            canvas.FillPath(reg);
        }
    }
}

