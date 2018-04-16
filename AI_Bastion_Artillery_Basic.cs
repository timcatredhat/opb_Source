/// /// <summary>
/// THIS SCRIPT DETERMINES SIMPLE AI BEHAVIOUR FOR BASTION ONLY ATTACK (TANK)
/// GO TO BASTION, ATTACK IT. RANGED AT FIRST, THEN UP CLOSE. JUST TAKE DAMAGE BY PLAYERS (YOU HAVE LOTS OF HEALTH)
/// ORKTANK
/// </summary>

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityRandom = UnityEngine.Random;

public class AI_Bastion_Artillery_Basic : Enemy_parent_script
{
    public GameObject warningreticle, warningobj;
    private ArtilleryTankPack toHandle;
    private DateTime lastSync;

    public override void Start()
    {
        base.Start();
        gameObject.name = "Artillery " + networkID;

        agent = gameObject.GetComponent<NavMeshAgent>();
        tankmyroot = gameObject.GetComponent<Root>();
        Invoke("ReadyAction", tankmyroot.atkSpeed);

        Network.OnReceive += OnReceive;
        tankmyroot.OnDeath += OnDeath;
        tankshot = gameObject.GetComponent<AudioSource>();
        isroaming = false;
        droplocation = RandomCircle(bastion.transform.position, 13);

        InvokeRepeating("Switch", 10, 10); isroaming = false; //to switch between roaming and shooting
        InvokeRepeating("randloc", 0, 20); //change the random wander location
        InvokeRepeating("randbombloc", 0, 20); //change the random bomb drop location

        warningobj = Instantiate(warningreticle, droplocation, Quaternion.identity); //warning reticle UI
    }

    public override void Update()
    {
        base.Update();

        if (GetComponent<Root>().HP > 0)
        {
            lastpos = transform.position;
        }
        if (isroaming)
        {
            warningobj.SetActive(false);
        }
        else
        {
            warningobj.SetActive(true);
        }

        warningobj.transform.position = droplocation; //reticle warning UI

        artillery_tank_attack(); //from parent

        if (toHandle != null)
        {
            droplocation = toHandle.DropLocation;
            wanderlocation = toHandle.WanderLocation;

            agent.enabled = false;
            transform.position = toHandle.CurrentPosition;
            agent.enabled = true;
            agent.nextPosition = toHandle.CurrentPosition;

            agent.SetDestination(wanderlocation);

            toHandle = null;
            lastUpdate = DateTime.Now;
            hasSentPacket = false;
        }

        if (Network.IsHost && (DateTime.Now - lastSync).TotalMilliseconds > Network.UpdateInterval)
        {
            ArtilleryTankPack pack = new ArtilleryTankPack(droplocation, wanderlocation, networkID, transform.position);
            Network.Send(pack);

            lastSync = DateTime.Now;
            hasSentPacket = false;
        }
    }

    void OnReceive(NetworkPacket pack, string id)
    {
        if (pack.Header == PacketType.ArtilleryTankPack)
        {
            ArtilleryTankPack packet = (ArtilleryTankPack)pack;

            if (packet.ArtilleryID == this.networkID)
            {
                toHandle = packet;

                wave_control.Instance.toReactivate.Enqueue(gameObject);
            }
        }
    }

    void OnDeath(Root root, string killer)
    {
        warningobj.SetActive(false);

        if (killer != null)
        {
            Stats.PlayerStats[killer].TankKillz++;
            Stats.PlayerStats[killer].TokenContributed += tokenValue;
            tokenpopup(killer == Network.LocalID);
        }

        Network.OnReceive -= OnReceive;
        Destroy(gameObject);
    }
}
