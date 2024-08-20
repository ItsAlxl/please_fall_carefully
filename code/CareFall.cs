using System;

public sealed class CareFall
{
	private CareFall() { }
	private static readonly Lazy<CareFall> lazy = new( () => new CareFall() );

	public static CareFall Game { get { return lazy.Value; } }

	public const int SCORE_BUMP_AMT = 10;
	public const int SCORE_SQUEEZE_AMT = 250;
	public const int SCORE_STAGE_AMT = 2500;

	public Player plr;
	public StageController stages;

	public delegate void RestartEventHandler();
	public event RestartEventHandler RestartEvent;
	public void RaiseRestartEvent()
	{
		RestartEvent?.Invoke();
	}
}