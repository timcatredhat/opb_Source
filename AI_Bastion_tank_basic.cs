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

public class AI_Bastion_tank_basic : Enemy_parent_script
{
    private AITankPack toHandle;
    private DateTime lastSync;

    public override void Start()
    {
        base.Start();
        gameObject.name = "Tank " + networkID;

        agent = gameObject.GetComponent<NavMeshAgent>();
        tankmyroot = gameObject.GetComponent<Root>();
        Invoke("ReadyAction", tankmyroot.atkSpeed);

        Network.OnReceive += OnReceive;
        tankmyroot.OnDeath += OnDeath;
        tankshot = gameObject.GetComponent<AudioSource>();

        readytoroam = false;
        isroaming = false;
        InvokeRepeating("Switch", 0, 4); //switch between roaming and shooting
        InvokeRepeating("randloc", 0, 8); //change roam location
    }

    public override void Update()
    {
        base.Update();

        if (execute)
        {
            if (GetComponent<Root>().HP > 0)
            {
                lastpos = transform.position;
            }
            buggy_attack_target(); //from parent 
        }

        if (toHandle != null)
        {
            agent.enabled = false;
            transform.position = toHandle.Position;
            agent.enabled = true;
            agent.nextPosition = toHandle.Position;

            wanderlocation = toHandle.RandomOffsetPosition;

            if (readytoroam)
                agent.SetDestination(wanderlocation);
            else
                agent.SetDestination(bastion.transform.position);

            toHandle = null;
            lastUpdate = DateTime.Now;
            hasSentPacket = false;
        }

        if (Network.IsHost && (DateTime.Now - lastSync).TotalMilliseconds > Network.UpdateInterval)
        {
            AITankPack pack = new AITankPack(transform.position, networkID, wanderlocation);
            Network.Send(pack);

            lastSync = DateTime.Now;
            hasSentPacket = false;
        }
    }

    void OnReceive(NetworkPacket pack, string id)
    {
        if (pack.Header == PacketType.AITankPack)
        {
            AITankPack packet = (AITankPack)pack;

            if (packet.TankID == this.networkID)
            {
                this.toHandle = packet;

                wave_control.Instance.toReactivate.Enqueue(gameObject);
            }
        }
    }

    void OnDeath(Root root, string killer)
    {
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