/// /// <summary>
/// THIS SCRIPT REGENERATES PLAYER HEALTH GRADUALLY
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player_health_regen : MonoBehaviour {
	public Root myroot;
	public float nextTime;

	void Start () {
		myroot = GetComponent<Root> ();						//PLAYER root file
		nextTime = Time.time;								//iteration interval for refilling health
	}
	
	void Update () {
		if (Time.time > nextTime) {							//iteration interval
			if (myroot.HP <= 985 && myroot.HP > 10) {							//prevent health overflow
				myroot.GetHeals (15);						//heal PLAYER by 20 points every 20 seconds unless at full health
			}
			if (myroot.HP == 995) {
				myroot.GetHeals (5);
			}
			nextTime = Time.time + 1.5f;
		}
	}
}