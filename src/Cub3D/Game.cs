namespace Cub3D;

public readonly record struct InputState(
    bool Forward,
    bool Back,
    bool StrafeLeft,
    bool StrafeRight,
    bool TurnLeft,
    bool TurnRight,
    float MouseDeltaX = 0,
    bool ToggleMinimap = false,
    bool ToggleShading = false,
    bool UseDoor = false,
    bool Run = false);

public sealed class Game
{
    public const double MoveSpeed = 3.0;

    public const double RunMultiplier = 2.0;

    public const double TurnSpeed = 2.5;

    public const double MouseSensitivity = 0.0025;

    private readonly Renderer _renderer = new();

    public Game(Scene scene, TextureSet textures, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Scene = scene;
        Textures = textures;
        World = new World(scene.Map);
        Player = Player.FromScene(scene);
        Frame = new FrameBuffer(width, height);

        if (EasterEgg.PlaceChicken(scene) is PlacedModel chicken)
        {
            Chicken = chicken;
            _models = [chicken];
            World.AddObstacle(chicken.X, chicken.Y, ChickenRadius);
        }
    }

    public const double ChickenRadius = 0.35;

    private readonly PlacedModel[] _models = [];

    public PlacedModel? Chicken { get; }

    public Scene Scene { get; }

    public TextureSet Textures { get; }

    public World World { get; }

    public Player Player { get; }

    public FrameBuffer Frame { get; }

    public RenderOptions Options { get; } = new();

    public void Update(InputState input, double dt)
    {
        dt = Math.Clamp(dt, 0, 0.1);

        if (input.ToggleMinimap)
        {
            Options.Minimap = !Options.Minimap;
        }

        if (input.ToggleShading)
        {
            Options.Shading = !Options.Shading;
        }

        if (input.UseDoor)
        {
            World.ToggleDoor(Player);
        }

        World.Update(dt);

        double turn = ((input.TurnRight ? 1 : 0) - (input.TurnLeft ? 1 : 0)) * TurnSpeed * dt;
        turn += input.MouseDeltaX * MouseSensitivity;
        if (turn != 0)
        {
            Player.Rotate(turn);
        }

        double forward = (input.Forward ? 1 : 0) - (input.Back ? 1 : 0);
        double strafe = (input.StrafeRight ? 1 : 0) - (input.StrafeLeft ? 1 : 0);
        if (forward != 0 || strafe != 0)
        {
            double speed = input.Run ? MoveSpeed * RunMultiplier : MoveSpeed;
            double norm = speed * dt / Math.Sqrt((forward * forward) + (strafe * strafe));
            Player.Walk(forward * norm, strafe * norm, World.IsWalkable, World.IsFree);
        }
    }

    public void Render() => _renderer.Render(World, Player, Scene, Textures, Frame, Options, _models);
}
