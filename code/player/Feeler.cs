using System;

[Group( "CareFall" )]
[Title( "Feeler" )]
[Icon( "waving_hand" )]
public sealed class Feeler : Component, Component.ITriggerListener
{
	private int scrapeCount = 0;
	private Player plr;

	protected override void OnAwake()
	{
		plr = GameObject.Parent.Components.Get<Player>();
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		scrapeCount++;
		if ( plr.IsFlying() )
		{
			var go = other.GameObject;
			var tags = go.Tags;
			BumpMemory bumpMem;
			if ( tags.Has( "pfc-bumped" ) )
			{
				bumpMem = go.Components.Get<BumpMemory>();
			}
			else
			{
				bumpMem = go.Components.Create<BumpMemory>();
				PFC.Game.ScoreBumps++;
			}
			bumpMem.StartScrape();
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		scrapeCount--;

		if ( other.GameObject.Tags.Has( "pfc-bumped" ) )
		{
			other.GameObject.Components.Get<BumpMemory>().EndScrape();
		}
	}

	public bool IsScraping()
	{
		return scrapeCount > 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;
		if ( IsScraping() && plr.IsFlying() )
		{
			PFC.Game.ScoreScrapes += Time.Delta * (float)Math.Pow( plr.Speed, 1.5 ) * 0.0001f;
		}
	}
}
