using System.Numerics;
using System.Runtime.CompilerServices;
using Raylib_cs;

namespace Bungus.Game;

public sealed class Obstacle(Rectangle rect)
{
    public Rectangle Rect { get; } = rect;
}
