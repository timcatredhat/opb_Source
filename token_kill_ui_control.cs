using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class token_kill_ui_control : MonoBehaviour {
	public bool atapex;
	// Use this for initialization
	void Start () {
		atapex = false;
		Invoke ("change", 0.8f);
		Destroy(gameObject, 1.4f);
	}
	
	// Update is called once per frame
	void Update () {
		if (gameObject != null) {
			transform.Translate (Vector3.up * 2.3f * Time.deltaTime);
			if (atapex) {
				GetComponent<CanvasGroup> ().alpha -= 0.03f;
			}
		}
	}

	void change() {
		atapex = true;
	}
}
