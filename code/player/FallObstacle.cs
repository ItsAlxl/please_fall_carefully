[Group( "CareFall" )]
[Title( "(don't use in editor) Fall Obstacle" )]
[Icon( "disabled_by_default" )]
public sealed class FallObstacle : Component
{
	const float BUMP_R = 0.0f;
	const float BUMP_G = 0.0f;
	const float BUMP_B = 1.0f;
	const float SCRAPE_R = 0.0f;
	const float SCRAPE_G = 0.5f;
	const float SCRAPE_B = 1.0f;
	const float TINT_STR = 0.5f;

	private class ModelData
	{
		public Color original;
		public Color scraping;
		public Color bumped;

		public ModelData( Color original_tint )
		{
			original = original_tint;
			scraping = GetTintedColor( SCRAPE_R, SCRAPE_G, SCRAPE_B );
			bumped = GetTintedColor( BUMP_R, BUMP_G, BUMP_B );
		}

		private Color GetTintedColor( float r, float g, float b )
		{
			return new Color(
				original.r + ((r - original.r) * TINT_STR),
				original.g + ((g - original.g) * TINT_STR),
				original.b + ((b - original.b) * TINT_STR)
			);
		}
	}

	private class ColliderData
	{
		public bool scraping = false;
	}

	private readonly Dictionary<ModelRenderer, ModelData> model_data = new( 1 );
	private readonly Dictionary<Collider, ColliderData> col_data = new( 1 );

	protected override void OnAwake()
	{
		foreach ( var m in GameObject.Components.GetAll<ModelRenderer>() )
		{
			model_data[m] = new ModelData( m.Tint );
		}
		foreach ( var c in GameObject.Components.GetAll<Collider>() )
		{
			col_data[c] = new ColliderData();
		}
	}

	public bool IsScraping()
	{
		return col_data.Any( kvp => kvp.Value.scraping );
	}

	public bool StartScrape( Collider col )
	{
		GameObject.Tags.Add( "pfc-bumped" );

		bool newScrape = !IsScraping();
		col_data[col].scraping = true;

		if ( newScrape )
		{
			foreach ( var kvp in model_data )
			{
				kvp.Key.Tint = kvp.Value.scraping;
			}
		}
		return newScrape;
	}

	public bool EndScrape( Collider col )
	{
		bool endScrape = IsScraping();
		col_data[col].scraping = false;
		endScrape = endScrape && !IsScraping();

		if ( endScrape )
		{
			foreach ( var kvp in model_data )
			{
				kvp.Key.Tint = kvp.Value.bumped;
			}
		}
		return endScrape;
	}

	public void Restore()
	{
		GameObject.Tags.Remove( "pfc-bumped" );
		foreach ( var kvp in model_data )
		{
			kvp.Key.Tint = kvp.Value.original;
		}
		foreach ( var kvp in col_data )
		{
			kvp.Value.scraping = false;
		}
	}
}