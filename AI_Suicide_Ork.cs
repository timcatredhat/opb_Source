/// /// <summary>
/// THIS SCRIPT DETERMINES SIMPLE AI BEHAVIOUR FOR ORK BOMBAZ
/// ORK BOMBAZ
/// </summary>

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityRandom = UnityEngine.Random;

public class AI_Suicide_Ork : Enemy_parent_script, IResetable
{
    private SuicideOrkPack toHandle;
    private DateTime lastSync;

    public override void Start()
    {
        base.Start();

        gameObject.name = "Exploda " + networkID;

        agent = gameObject.GetComponent<NavMeshAgent>();
        myroot = gameObject.GetComponent<Root>();

        Network.OnReceive += OnReceive;
        myroot.OnDeath += OnDeath;
    }

    public override void Update()
    {
        base.Update();

        if (execute)
        {
            if (gameObject.transform.position.y <= -50)
            {
                gameObject.GetComponent<Root>().HP -= 10;
            }
            if (GetComponent<Root>().HP > 0)
            {
                lastpos = transform.position;
            }
            playerobjs = GameObject.FindGameObjectsWithTag("Player");


            suicide_ork_attack(); //from parent
        }
        else
        {
            GetComponent<Rigidbody>().AddForce(new Vector3(0, -50, 0));
        }

        if (toHandle != null)
        {
            explodelocation = toHandle.ExplodeLocation;

            agent.enabled = false;
            transform.position = toHandle.CurrentPosition;
            agent.enabled = true;
            agent.nextPosition = toHandle.CurrentPosition;

            toHandle = null;
            lastUpdate = DateTime.Now;
            hasSentPacket = false;
        }

        if ((DateTime.Now - lastSync).TotalMilliseconds > Network.UpdateInterval)
        {
            SuicideOrkPack pack = new SuicideOrkPack(explodelocation, networkID, transform.position);
            Network.Send(pack);

            lastSync = DateTime.Now;
            hasSentPacket = false;
        }
    }

    void OnReceive(NetworkPacket pack, string id)
    {
        if (pack.Header == PacketType.SuicideOrkPack)
        {
            SuicideOrkPack packet = (SuicideOrkPack)pack;

            if (packet.ExplodaID == this.networkID)
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
            Stats.PlayerStats[killer].SuicideKillz++;
            Stats.PlayerStats[killer].TokenContributed += tokenValue;
            tokenpopup(killer == Network.LocalID);
        }

        Destroy(gameObject);
        Network.OnReceive -= OnReceive;
    }
}
