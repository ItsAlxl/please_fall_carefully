[Group( "CareFall" )]
[Title( "(don't use in editor) Bump Memory" )]
[Icon( "disabled_by_default" )]
public sealed class BumpMemory : Component
{
	const float BUMP_R = 0.0f;
	const float BUMP_G = 0.0f;
	const float BUMP_B = 1.0f;
	const float SCRAPE_R = 0.0f;
	const float SCRAPE_G = 0.5f;
	const float SCRAPE_B = 1.0f;
	const float TINT_STR = 0.5f;

	private class BMData
	{
		public Color original;
		public Color scraping;
		public Color bumped;

		public BMData( Color original_tint )
		{
			original = original_tint;
			scraping = GetTintedColor( SCRAPE_R, SCRAPE_G, SCRAPE_B );
			bumped = GetTintedColor( BUMP_R, BUMP_G, BUMP_B );
		}

		private Color GetTintedColor( float r, float g, float b )
		{
			return new Color(
				original.r + (r - original.r) * TINT_STR,
				original.g + (g - original.g) * TINT_STR,
				original.b + (b - original.b) * TINT_STR
			);
		}
	}

	private Dictionary<ModelRenderer, BMData> data = new( 1 );

	protected override void OnAwake()
	{
		var ms = GameObject.Components.GetAll<ModelRenderer>();
		foreach ( var m in ms )
		{
			data[m] = new BMData( m.Tint );
		}
	}

	public void StartScrape()
	{
		GameObject.Tags.Add( "pfc-bumped" );
		foreach ( var kvp in data )
		{
			kvp.Key.Tint = kvp.Value.scraping;
		}
	}

	public void EndScrape()
	{
		foreach ( var kvp in data )
		{
			kvp.Key.Tint = kvp.Value.bumped;
		}
	}

	public void MakeUnbumped()
	{
		GameObject.Tags.Remove( "pfc-bumped" );
		foreach ( var kvp in data )
		{
			kvp.Key.Tint = kvp.Value.original;
		}
	}
}