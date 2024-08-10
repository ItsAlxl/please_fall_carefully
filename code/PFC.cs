using System;

public sealed class PFC
{
	private PFC() { }
	private static readonly Lazy<PFC> lazy =
		new Lazy<PFC>( () => new PFC() );

	public static PFC Game { get { return lazy.Value; } }

	public int ScoreBumps;
	public float ScoreScrapes;
}