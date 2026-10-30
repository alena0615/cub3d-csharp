using System.Diagnostics;
using System.Globalization;
using Raylib_cs;

namespace Cub3D;

public static class GameWindow
{
    private const int MouseWarmupFrames = 10;

    private const float MaxMouseStep = 150;

    public static void Run(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        FrameBuffer frame = game.Frame;

        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.InitWindow(frame.Width, frame.Height, "cub3D");
        if (!Raylib.IsWindowReady())
        {
            throw new CubError("не удалось открыть окно");
        }

        Raylib.SetExitKey(KeyboardKey.Escape);
        Raylib.DisableCursor();
        Raylib.SetTargetFPS(0);

        Image image = Raylib.GenImageColor(frame.Width, frame.Height, Color.Black);
        Texture2D screen = Raylib.LoadTextureFromImage(image);
        Raylib.UnloadImage(image);

        var renderWatch = new Stopwatch();
        var wallClock = Stopwatch.StartNew();
        double renderMsSum = 0;
        int frames = 0;
        int totalFrames = 0;

        try
        {
            while (!Raylib.WindowShouldClose())
            {
                float dt = Raylib.GetFrameTime();

                bool mouseReady = totalFrames >= MouseWarmupFrames;
                game.Update(ReadInput(mouseReady), dt);

                renderWatch.Restart();
                game.Render();
                renderWatch.Stop();
                renderMsSum += renderWatch.Elapsed.TotalMilliseconds;
                frames++;
                totalFrames++;

                Raylib.UpdateTexture(screen, frame.Pixels);
                Raylib.BeginDrawing();
                Raylib.DrawTexture(screen, 0, 0, Color.White);
                Raylib.EndDrawing();

                double elapsed = wallClock.Elapsed.TotalSeconds;
                if (elapsed >= 0.5)
                {
                    string title = string.Create(
                        CultureInfo.InvariantCulture,
                        $"cub3D — {frames / elapsed:F0} FPS, кадр {elapsed * 1000 / frames:F2} мс (построение {renderMsSum / frames:F2} мс)  [M] карта [F] тени [E] дверь [Shift] бег");
                    Raylib.SetWindowTitle(title);
                    wallClock.Restart();
                    renderMsSum = 0;
                    frames = 0;
                }
            }
        }
        finally
        {
            Raylib.UnloadTexture(screen);
            Raylib.CloseWindow();
        }
    }

    private static InputState ReadInput(bool mouseReady)
    {
        System.Numerics.Vector2 delta = Raylib.GetMouseDelta();
        float mouseX = mouseReady ? Math.Clamp(delta.X, -MaxMouseStep, MaxMouseStep) : 0;
        return new InputState(
            Forward: Raylib.IsKeyDown(KeyboardKey.W),
            Back: Raylib.IsKeyDown(KeyboardKey.S),
            StrafeLeft: Raylib.IsKeyDown(KeyboardKey.A),
            StrafeRight: Raylib.IsKeyDown(KeyboardKey.D),
            TurnLeft: Raylib.IsKeyDown(KeyboardKey.Left),
            TurnRight: Raylib.IsKeyDown(KeyboardKey.Right),
            MouseDeltaX: mouseX,
            ToggleMinimap: Raylib.IsKeyPressed(KeyboardKey.M),
            ToggleShading: Raylib.IsKeyPressed(KeyboardKey.F),
            UseDoor: Raylib.IsKeyPressed(KeyboardKey.E),
            Run: Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift));
    }
}
