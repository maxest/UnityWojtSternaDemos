using UnityEngine;

public class CustomShadowMap : MonoBehaviour
{
	public CustomShadowMapLight myLight;
	public GeometryHierarchy geometryHierarchy;

	void Start()
	{
		geometryHierarchy.myLight = myLight;
	}
}
