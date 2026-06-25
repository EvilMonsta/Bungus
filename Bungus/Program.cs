namespace Bungus.Game;

public static class Program
{
    public static void Main()
    {
        using var game = new SciFiRogueGame();
        game.Run();
    }
}
