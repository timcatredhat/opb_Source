/// /// <summary>
/// THIS SCRIPT DETERMINES INTELLIGENT AI BEHAVIOUR FOR PLAYER VS. BASTION CHOICES (RANGED -> MELEE IF TOO CLOSE)
/// IF UNDER ATTACK AT BASTION, AI WILL FIGHT BACK EITHER MELEE OR RANGED, DEPENDING ON NATURE OF ATTACK
/// ROKKIT BOYZ
/// </summary>

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

using UnityRandom = UnityEngine.Random;

public class AI_PlayerVBastion_intelligent : Enemy_parent_script
{
    private DateTime lastSync;
    private AIPlayerVBastionPack toHandle;

    public override void Start()
    {
        base.Start();

        gameObject.name = "''Intelligent'' " + networkID;

        lastSync = default(DateTime);
        Network.OnReceive += OnReceive;

        agent = GetComponent<NavMeshAgent>();

        myroot = gameObject.GetComponent<Root>();
        currplayerobj = playerobjs[0];
        currplayerobjroot = currplayerobj.GetComponent<Root>();

        Invoke("ReadyAction", myroot.atkSpeed);


        myroot.OnDeath += OnDeath;
        muzzlelight = GetComponent<MuzzleFlash>();
    }

    public override void Update()
    {
        base.Update();

        if (execute)
        {
            playerobjs = GameObject.FindGameObjectsWithTag("Player");
            if (GetComponent<Root>().HP > 0)
            {
                lastpos = transform.position;
            }
            find_closest_player();

            if (currgate == null)
            {
                if (Vector3.Distance(transform.position, currplayerobj.transform.position) > 9f)
                {
                    attack_bastion_bool = true;
                    attack_player_bool = false;
                }
                else
                {
                    attack_player_bool = true;
                    attack_bastion_bool = false;
                }
            }
            else
            {
                attack_bastion_bool = true;
                attack_player_bool = false;
            }


            choose_target_intelligent(); //from parent
            attack_player_intelligent(); //from parent
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

            if (toHandle.AttackingBastion)
            {
                agent.SetDestination(bastion.transform.position);
                attack_bastion_bool = true;
            }
            else
            {
                GameObject go = GameObject.Find(toHandle.PlayerId);

                currplayerobjroot = go.GetComponent<Root>();
                currplayerobj = go;

                agent.SetDestination(currplayerobjroot.transform.position);
                attack_bastion_bool = false;
            }

            toHandle = null;
            lastUpdate = DateTime.Now;
            hasSentPacket = false;
        }

        if (Network.IsHost && (DateTime.Now - lastSync).TotalMilliseconds > Network.UpdateInterval)
        {
            AIPlayerVBastionPack pack = new AIPlayerVBastionPack(transform.position, networkID, attack_bastion_bool, attack_bastion_bool ? null : currplayerobjroot.GetComponent<PlayerControl>().ID);
            Network.Send(pack);

            lastSync = DateTime.Now;
        }
    }

    void OnReceive(NetworkPacket pack, string id)
    {
        if (pack.Header == PacketType.AIPlayerVBastionPack)
        {
            AIPlayerVBastionPack packet = (AIPlayerVBastionPack)pack;

            if (packet.AIID == this.networkID)
            {
                toHandle = packet;

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