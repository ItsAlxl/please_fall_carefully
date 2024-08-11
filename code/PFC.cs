using System;

public sealed class CareFall
{
	private CareFall() { }
	private static readonly Lazy<CareFall> lazy = new( () => new CareFall() );

	public static CareFall Game { get { return lazy.Value; } }

	public Player plr;
}