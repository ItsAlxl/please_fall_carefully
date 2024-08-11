using System;

public sealed class PFC
{
	private PFC() { }
	private static readonly Lazy<PFC> lazy = new( () => new PFC() );

	public static PFC Game { get { return lazy.Value; } }

	public Player plr;
}