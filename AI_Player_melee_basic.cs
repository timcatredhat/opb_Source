/// /// <summary>
/// THIS SCRIPT DETERMINES SIMPLE AI BEHAVIOUR FOR PLAYER ONLY ATTACK (MELEE)
/// SLUGGA BOYZ
/// </summary>

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

using UnityRandom = UnityEngine.Random;

public class AI_Player_melee_basic : Enemy_parent_script
{
    private AIMeleeBasicPack toHandle;
    private DateTime lastSync;                                                                          // Last time we synced with the cloud

    public override void Start()
    {
        base.Start();

        gameObject.name = "Melee " + networkID;

        agent = GetComponent<NavMeshAgent>();
        myroot = gameObject.GetComponent<Root>();
        Invoke("ReadyAction", myroot.atkSpeed); //attack speed

        lastSync = default(DateTime);
        Network.OnReceive += OnReceive;
        myroot.OnDeath += OnDeath;

        currplayerobj = playerobjs[0];
        currplayerobjroot = currplayerobj.GetComponent<Root>();
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
            playerobjs = GameObject.FindGameObjectsWithTag("Player");


            find_closest_player(); //from parent
            melee_attack_player(); //from parent 
        }
        else
        {
            GetComponent<Rigidbody>().AddForce(new Vector3(0, -50, 0));
        }

        if (toHandle != null)
        {
            agent.enabled = false;
            transform.position = toHandle.Position;
            agent.enabled = true;
            agent.nextPosition = toHandle.Position;

            if (currplayerobj.GetComponent<PlayerControl>().ID != toHandle.TargetID)
            {
                var temp = GameObject.FindObjectsOfType<PlayerControl>();

                foreach (var player in temp)
                {
                    if (player.ID == toHandle.TargetID)
                    {
                        agent.SetDestination(player.transform.position);

                        currplayerobj = player.gameObject;
                        currplayerobjroot = player.GetComponent<Root>();
                    }
                }
            }

            toHandle = null;
            lastUpdate = DateTime.Now;
            hasSentPacket = false;
        }

        if (Network.IsHost && (DateTime.Now - lastSync).TotalMilliseconds > Network.UpdateInterval)
        {
            AIMeleeBasicPack pack = new AIMeleeBasicPack(transform.position, currplayerobj.GetComponent<PlayerControl>().ID, networkID);
            Network.Send(pack);

            lastSync = DateTime.Now;
            hasSentPacket = false;
        }
    }

    void OnReceive(NetworkPacket packet, string id)
    {
        if (packet.Header == PacketType.AIMeleeBasicPack)
        {
            AIMeleeBasicPack pack = (AIMeleeBasicPack)packet;

            if (pack.AIID == this.networkID)
            {
                toHandle = pack;

                wave_control.Instance.toReactivate.Enqueue(gameObject);
            }
        }
    }

    void OnDeath(Root root, string killer)
    {
        if (killer != null)
        {
            Stats.PlayerStats[killer].OrcKillz++;
            Stats.PlayerStats[killer].TokenContributed += tokenValue;
            tokenpopup(killer == Network.LocalID);
        }

        Destroy(gameObject);
        Network.OnReceive -= OnReceive;
    }
}