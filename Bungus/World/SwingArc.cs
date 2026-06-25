using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class SwingArc
{
    private readonly List<object> _hitTargets = [];
    private readonly Vector2 _originOffset;
    private readonly Vector2 _lineStartOffset;
    private readonly Vector2 _lineEndOffset;

    public Vector2 Origin { get; private set; }
    public float Radius { get; }
    public float AngleStart { get; }
    public float AngleEnd { get; }
    public float Life { get; set; }
    public float MaxLife { get; }
    public Color Color { get; }
    public bool IsLine { get; }
    public Vector2 LineStart { get; private set; }
    public Vector2 LineEnd { get; private set; }
    public bool ReverseSweep { get; }
    public float DashLengthRatio { get; }
    public SwingVisualStyle VisualStyle { get; }
    public float Progress => MaxLife <= 0f ? 1f : 1f - Math.Clamp(Life / MaxLife, 0f, 1f);

    private SwingArc(Vector2 anchorPosition, Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color, bool reverseSweep)
    {
        Origin = origin;
        _originOffset = origin - anchorPosition;
        Radius = radius;
        AngleStart = angleStart;
        AngleEnd = angleEnd;
        Life = life;
        MaxLife = life;
        Color = color;
        ReverseSweep = reverseSweep;
        VisualStyle = SwingVisualStyle.ArcSlash;
    }

    private SwingArc(Vector2 anchorPosition, Vector2 lineStart, Vector2 lineEnd, float life, Color color, float dashLengthRatio)
    {
        IsLine = true;
        LineStart = lineStart;
        LineEnd = lineEnd;
        _lineStartOffset = lineStart - anchorPosition;
        _lineEndOffset = lineEnd - anchorPosition;
        Life = life;
        MaxLife = life;
        Color = color;
        DashLengthRatio = dashLengthRatio;
        VisualStyle = SwingVisualStyle.SpearThrust;
    }

    public static SwingArc Arc(Vector2 anchorPosition, Vector2 origin, float radius, float angleStart, float angleEnd, float life, Color color, bool reverseSweep = false)
        => new(anchorPosition, origin, radius, angleStart, angleEnd, life, color, reverseSweep);

    public static SwingArc Line(Vector2 anchorPosition, Vector2 lineStart, Vector2 lineEnd, float life, Color color, float dashLengthRatio = 0.4f)
        => new(anchorPosition, lineStart, lineEnd, life, color, dashLengthRatio);

    public void UpdateAnchor(Vector2 anchorPosition)
    {
        if (IsLine)
        {
            LineStart = anchorPosition + _lineStartOffset;
            LineEnd = anchorPosition + _lineEndOffset;
            return;
        }

        Origin = anchorPosition + _originOffset;
    }

    public bool TryRegisterHit(object target)
    {
        if (_hitTargets.Contains(target)) return false;
        _hitTargets.Add(target);
        return true;
    }
}
