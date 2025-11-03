using Godot;

namespace Incrememental.scripts.levels;

/// <summary>
/// Manages level state including economy and timers.
/// Uses C# events instead of Godot signals to avoid marshalling overhead.
/// </summary>
[GlobalClass]
public partial class LevelNew : Node
{
    // C# event instead of Godot signal
    public event System.Action<int> MoneyUpdated;

    [Export] public float MoneyInterval { get; set; } = 0.2f;

    public int MoneyCount { get; private set; } = 0;

    private Timer _moneyTimer;

    public override void _Ready()
    {
        _moneyTimer = new Timer
        {
            WaitTime = MoneyInterval
        };
        _moneyTimer.Timeout += OnMoneyTimerTimeout;
        AddChild(_moneyTimer);
        StartMoneyTimer();
    }

    /// <summary>
    /// Starts the money generation timer.
    /// </summary>
    public void StartMoneyTimer()
    {
        _moneyTimer?.Start();
    }

    /// <summary>
    /// Stops the money generation timer.
    /// </summary>
    public void StopMoneyTimer()
    {
        _moneyTimer?.Stop();
    }

    private void OnMoneyTimerTimeout()
    {
        MoneyCount++;
        MoneyUpdated?.Invoke(MoneyCount);
    }
}
