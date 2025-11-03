using Godot;

namespace Incrememental.scripts.entities.buildings;

/// <summary>
/// Headquarters building that generates income and provides research options.
/// </summary>
[GlobalClass]
public partial class HQBuilding : Building
{
    [Export] public float IncomeRate { get; set; } = 1.0f;
    [Export] public float TimerWaitTime { get; set; } = 1.0f;

    private Timer _timer;

    public override void _Ready()
    {
        base._Ready();

        // Get Timer child node
        _timer = GetNode<Timer>("Timer");
        if (_timer != null)
        {
            _timer.Timeout += OnTimerTimeout;
            _timer.Start();
        }
    }

    /// <summary>
    /// Opens the research UI for this HQ building.
    /// </summary>
    public void OpenResearchUI()
    {
        // TODO: Implement research logic
    }

    private void OnTimerTimeout()
    {
        // TODO: Implement money/income logic
    }
}
