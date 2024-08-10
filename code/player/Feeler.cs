[Group( "CareFall" )]
[Title( "Feeler" )]
[Icon( "waving_hand" )]
public sealed class Feeler : Component, Component.ITriggerListener
{
	public int feelCount = 0;

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		feelCount++;

		var rs = other.GameObject.Components.GetAll<ModelRenderer>();
		foreach ( var r in rs )
		{
			r.Tint = Color.Red;
		}
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		feelCount--;

		var rs = other.GameObject.Components.GetAll<ModelRenderer>();
		foreach ( var r in rs )
		{
			r.Tint = Color.Blue;
		}
	}
}
