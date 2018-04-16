using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class no_fog_minimap : MonoBehaviour {

	bool doWeHaveFogInScene;

	private void Start() {
		doWeHaveFogInScene = RenderSettings.fog;
	}

	private void OnPreRender() {
		RenderSettings.fog = false;
	}
	private void OnPostRender() {
		RenderSettings.fog = doWeHaveFogInScene;
	}
}