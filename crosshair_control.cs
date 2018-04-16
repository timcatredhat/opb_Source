using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crosshair_control : MonoBehaviour {
	public GameObject[] player;
	public IControlScheme ControlScheme;             // The ControlScheme controlling this player
	public bool aimingdownsight;
	public GameObject currplayer;
	public Camera maincam;
	//public bool aimingdownsight;
	public GameObject bulletray;
	// Use this for initialization
	void Start () {
		//player = GameObject.FindGameObjectsWithTag ("Player");
		currplayer = transform.parent.gameObject;// player [0];
		maincam = Camera.main;
		currplayer.GetComponentInChildren<crosshair_control> ().bulletray.gameObject.SetActive (false);
	}
	
	// Update is called once per frame
	void Update () {
		//if (ControlScheme != null) {

		if (currplayer != null) {
		aimingdownsight = currplayer.GetComponent<PlayerControl>().aimingDownSight;


			//Cursor.visible = false;
			//Vector3 temp = Input.mousePosition;
			//temp.z = 10f; // Set this to be the distance you want the object to be placed in front of the camera.
			//this.transform.position = currplayer.GetComponent<PlayerControl>().aimDirection;//currplayer.transform.position + new Vector3(0, 0, 16) + 5 * currplayer.transform.forward;//Camera.main.ScreenToWorldPoint (currplayer.transform.position + new Vector3(0, 0, 10) + currplayer.transform.forward);
			this.transform.rotation = maincam.transform.rotation;

			if (aimingdownsight) {
				this.GetComponent<CanvasGroup> ().alpha = 0;
				currplayer.GetComponentInChildren<crosshair_control> ().bulletray.gameObject.SetActive (true);
			} else {
				this.GetComponent<CanvasGroup> ().alpha = 1;
				this.transform.rotation = maincam.transform.rotation;
				currplayer.GetComponentInChildren<crosshair_control> ().bulletray.gameObject.SetActive (false);

			}
		}

	}
}
//aimdirection