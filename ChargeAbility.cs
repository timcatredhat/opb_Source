using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
class ChargeAbility : Ability
{
    public GameObject ChargePrefab, player, explodeEffect;          // The charge ability's own object, the player object, and the effect whenever the charge explodes
    public string PlayerID_Thrower;                                 // The id of the player throwing the grenade
    public float timenow;                                           // The current Time.time, for determining when it's ready again
    public float spd;                                               // God knows what this actually is
    public bool chargemove = false, notneargate = true, charging = false; // Wether we can move, and if we're about to hit a gate, and if we're currently charging

    public void Update()
    {
        cooldown_rate = (float)(DateTime.Now - lastUse).TotalMilliseconds / Cooldown; // The rate at which cooldown occurs

		notneargate = true;
		Collider[] colliders = Physics.OverlapSphere(transform.position, 5);// Get nearby objects
		foreach (Collider nearbyObject in colliders) {
			if (nearbyObject.tag == "Gate") {
				notneargate = false;
			}
		}

        if (chargemove && Time.time <= timenow + 0.45f)
        { // whenever we can charge again
            spd += spd * 0.28f;                           // adds speed to our current speed, making us speed up over time
            Instantiate(explodeEffect, player.transform.position + Vector3.forward * 3 + Vector3.up * 4, transform.rotation);   // shows the explodeEffect

            player.transform.Translate(Vector3.forward * 10 * Time.deltaTime);    // Speeds us in the direction we're pointing the cursor

        }
        else
        {
            chargemove = false;                                                     // Tells us we're not charging anymore
            spd = 1f;                                                               // Resets our speed back to standard
        }
    }
    public override void UseAbility(Transform parent, Vector3 direction, string playerID, ref float power) // Called whenever a charge ability is desired by the user
    {
        if (notneargate)
        {      // Only charges if we're not within the perimiters of a gate, as we might otherwise blink through the gate
            spd = 1f;           // Resets our speed, in case it has not been done earlier

            if ((DateTime.Now - lastUse).TotalMilliseconds > Cooldown)
            {  // If we've waited long enough to start charging again
                charging = true;                                          // Tells everywhere else that we're currently charging

                if (power >= PowerCost)
                {                                 // If we have more power than what is needed to use this ability
                    power -= PowerCost;                                   // Subtracts the needed power from the player's sum of power
                    Instantiate(explodeEffect, player.transform.position + Vector3.forward * 3 + Vector3.up * 4, transform.rotation);   // shows the explodeEffect

                    getenemies();                                        // Gets all enemies within range of the effect
                    chargemove = true;                                    // Lets us know we're doing some kinda move
                    timenow = Time.time;                                  // The time we last did a charge at
                                                                          //player.transform.Translate (Vector3.forward * 245 * Time.deltaTime);

                    Instantiate(explodeEffect, player.transform.position + Vector3.forward * 3 + Vector3.up * 4, transform.rotation);   // shows the explodeEffect


                    //					if (AudioSources.Length > 0) {
                    //						source.clip = AudioSources [UnityEngine.Random.Range (0, AudioSources.Length)];
                    //						source.Play ();
                    //					}

                    lastUse = DateTime.Now;                               // When we last used this ability
                }
                else
                {
                    StartCoroutine(FadeItIn_noenergy());                // Starts fading in the no energy message, in case we on't have enough energy
                    StartCoroutine(FadeItOut_noenery());                // Fades said message out
                }
            }
            charging = false;                                             // We're no longer engaged in a charge
        }
    }

    void getenemies()
    {                                                   // Gets all the enemies near enough to be affected by the ability
        Collider[] colliders = Physics.OverlapSphere(player.transform.position, 10);// Get nearby objects
        foreach (Collider nearbyObject in colliders)
        {
            if (nearbyObject.tag == "Ork")
            {
                //nearbyObject.GetComponent<Rigidbody>().AddExplosionForce (60000, player.transform.position, 10f, 0);
                //nearbyObject.GetComponent<Root> ().grenadethrower = PlayerID_Thrower;

                nearbyObject.GetComponent<Root>().TakeDamege(15, Armour.Not_Specified, 0, PlayerID_Thrower);  // Gives whomever is unlucky enough to recieve it, 15 armour ignoring damage
                nearbyObject.GetComponent<Root>().StunIt();                                                   // Stuns the target of the ability
                Instantiate(explodeEffect, player.transform.position + Vector3.forward * 3 + Vector3.up * 4, transform.rotation);   // shows the explodeEffect
            }
        }
    }

}
