using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityRandom = UnityEngine.Random;
using UnityEngine.UI;

public class Enemy_parent_script : MonoBehaviour, IResetable
{
	public bool execute = true; //if a grenade is thrown on the bridge, execute is set to false to allow orks to fall off.		
    public Quaternion adjust; //to spawn objects at the right rotation
    public Vector3 lastpos; //a variable that stores the latest location of any given enemy
	public Vector3 bastion_offset;                                  
	public Vector3 wanderlocation, droplocation, explodelocation; //tank variables for random drifting and shell dropping
    public GameObject bastion, currgate, wave_one, tokenfromkill, suicidereticle; 
    public bool suicidetargetpicked, suicide_detected;
    public string suicidetarg; //the player to target
    public GameObject bullet, tankbullet, artillerybullet; //ammunition objects             
    public bool atkReady; //for shooting
    public NavMeshAgent agent;                        
    public float rotateSpeedWhenAtk = 8;           
    public float velocityRoto;
    public GameObject[] gates, playerobjs;
    public Root tankmyroot, bastionroot, playerroot, gateroot, myroot; //roots
    public GameObject currplayerobj, currweakestplayer;
    public Root currplayerobjroot;
    public List<Root> playerroots;
    public bool isCoroutineExecuting;

    public bool shootstaggered; //so ranged enemies shoot staggered rather than at the same time
    public bool readytoroam, isroaming;
    public AudioSource tankshot;
    public float wanderrad;

    public bool in_combat; 
    public bool attack_bastion_bool, attack_player_bool; //for the rokkit boy who needs to choose between the two targets
    public int tokenValue; //tokens earned when this enemy type is killed

    public MuzzleFlash muzzlelight;
    public ParticleSystem BulletRay; 

    private static int currentID;
    protected int networkID;

    [SerializeField]
    private GameObject BloodSplatterPrefab;

    [SerializeField]
    private Sprite[] BloodSplatters;

    private WaveOverPack waveOverPack = null;
    protected System.DateTime lastUpdate;
    protected bool hasSentPacket = false;

    ///
    /// INITIALIZATION BELOW
    ///

    public virtual void Start()
    {
        Terrain terrain = FindObjectOfType<Terrain>(); //terrain stuff for initialization

        if (terrain.terrainData.splatPrototypes.Length == 0)
        {
            terrain.terrainData.splatPrototypes = new SplatPrototype[BloodSplatters.Length];

            for (int i = 0; i < BloodSplatters.Length; i++)
            {
                terrain.terrainData.splatPrototypes[i] = new SplatPrototype()
                {
                    metallic = .5f,
                    texture = BloodSplatters[i].texture,
                };
            }
        }

        this.networkID = ++currentID;
        gameObject.name = "AI " + networkID;

        adjust.eulerAngles = new Vector3(0, 90, 0);

        playerobjs = GameObject.FindGameObjectsWithTag("Player"); //all players
        bastion = GameObject.FindGameObjectWithTag("bastion"); //the bastion
        bastion_offset = new Vector3(UnityRandom.Range(-10, 10), 0, UnityRandom.Range(-10, 10)); //the tight circumference around the bastion

        gates = GameObject.FindGameObjectsWithTag("Gate"); //the gates
        if (gates.Length > 0)
        {
            currgate = gates[0];
            foreach (var gate in gates)
            {
                float dist1 = Vector3.Distance(transform.position, currgate.transform.position);
                float dist2 = Vector3.Distance(transform.position, gate.transform.position);
                if (dist1 >= dist2)
                    currgate = gate;
            }
            gateroot = currgate.GetComponent<Root>();
            if (Vector3.Distance(currgate.transform.position, transform.position) > 
				Vector3.Distance(bastion.transform.position, transform.position))
            {
                currgate = null;
            }
        }
        else
        {
            currgate = null;
        }

        in_combat = false;                  

        attack_bastion_bool = false; 
        attack_player_bool = false;
        suicidetargetpicked = false;
        suicide_detected = false;

        GetComponent<Root>().OnDeath += OnDeath;
        lastUpdate = System.DateTime.Now;
    }

    void IResetable.Reset()
    {

    }

    ///
    /// ATTACK METHODS BELOW
    ///

    public virtual void Update()
    {
        if (waveOverPack != null) //makes all enemies disappear for all players when wave is over
        {
            Destroy(gameObject);
        }

        if (!Network.IsHost && (System.DateTime.Now - lastUpdate).TotalSeconds > (hasSentPacket ? 2 : 5))
        {
            switch (hasSentPacket)
            {
                case true:
                    gameObject.SetActive(false);
                    break;
                case false:
                    Network.Send(new AIRequestPacket(networkID));
                    hasSentPacket = true;
                    lastUpdate = System.DateTime.Now;
                    break;
            }
        }
    }

    public void buggy_attack_target() //this method controls the behaviour of the rokkit buggy
    {
        if (currgate == null)  //if gate has fallen
        {
            if (Vector3.Distance(transform.position, bastion.transform.position + bastion_offset) > 26 && !readytoroam)
            { 
                agent.isStopped = false;
                agent.SetDestination(bastion.transform.position);     
                lookatbastion();
            }
            else
            {                                                                
                readytoroam = true;
                if (!isroaming)
                {
                    agent.isStopped = true;
                    if (atkReady)
                    {
                        atkReady = false;
                        Invoke("ReadyAction", tankmyroot.atkSpeed);                                           
                        int dmg = tankmyroot.atk + UnityRandom.Range(-tankmyroot.atkVar, tankmyroot.atkVar + 1);
                        var aimdirection = bastion.transform.position - (transform.position + new Vector3(0, 3, 0));
                        GameObject missileClone = Instantiate(tankbullet, transform.position + 
							new Vector3(0, 3, 0), Quaternion.LookRotation(aimdirection) * adjust);
                        missileClone.GetComponent<TankBullet>().damage = dmg;
                        missileClone.GetComponent<TankBullet>().target = bastion.GetComponent<Root>();
                        tankshot.Play();
                    }
                }
                else if (isroaming) //drift around the bastion
                {
                    agent.isStopped = false;
                    agent.SetDestination(wanderlocation);
                    lookatbastion();
                }
            }
        }
        else //if the gate is still standing
        {
            if (Vector3.Distance(transform.position, currgate.transform.position) > 8)
            {
                agent.isStopped = false;
                agent.SetDestination(currgate.transform.position);                              
                lookatgate();
            }
            else
            {                                                           
                agent.isStopped = true;
                if (atkReady)
                {
                    atkReady = false;
                    Invoke("ReadyAction", tankmyroot.atkSpeed);         
                    int dmg = tankmyroot.atk + UnityRandom.Range(-tankmyroot.atkVar, tankmyroot.atkVar + 1);
                    var aimdirection = currgate.transform.position - (transform.position + new Vector3(0, 3, 0));
                    GameObject missileClone = Instantiate(tankbullet, transform.position + 
						new Vector3(0, 3, 0), Quaternion.LookRotation(aimdirection) * adjust);
                    missileClone.GetComponent<TankBullet>().damage = dmg;
                    missileClone.GetComponent<TankBullet>().target = gateroot;
                    tankshot.Play();
                }
            }
        }
    }

    public void artillery_tank_attack() //this method controls the behaviour of the artillery tank
    {
        if (!isroaming) //when it's ready to shoot shells
        {
            agent.isStopped = true;
            if (atkReady)
            {
                atkReady = false;
                Invoke("ReadyAction", tankmyroot.atkSpeed); 
                int dmg = tankmyroot.atk + UnityRandom.Range(-tankmyroot.atkVar, tankmyroot.atkVar + 1);
                var aimdirection = droplocation - (transform.position + new Vector3(0, 5, 0));
                GameObject missileClone = Instantiate(artillerybullet, transform.position + 
					new Vector3(0, 5, 0), Quaternion.LookRotation(aimdirection) * adjust);
                missileClone.GetComponent<ArtBullet>().damage = dmg;
                missileClone.GetComponent<ArtBullet>().target = droplocation;

                tankshot.Play();
            }
        }
        else if (isroaming && Vector3.Distance(transform.position, bastion.transform.position) >= 60) //if wandering around the map periphery
        {
            agent.isStopped = false;
            agent.SetDestination(wanderlocation);
            lookatbastion();
        }
        else if (isroaming && Vector3.Distance(transform.position, bastion.transform.position) < 60) //if tries to go too close to bastion it stops
        {
            agent.isStopped = true;
            isroaming = false;
        }
    }

    public void melee_attack_player() //this method controls the behaviour of the slugga boyz
    {
        if (currgate == null) //if the gate has fallen
        {
            if (Vector3.Distance(transform.position, currplayerobj.transform.position) <= 2f) //player is in range for attack
            {
                if (atkReady)
                {
                    lookatplayer();
                    agent.isStopped = true;
                    atkReady = false;
                    Invoke("ReadyAction", myroot.atkSpeed);     
                    int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);
                    currplayerobj.GetComponent<Root>().TakeDamege(dmg);   
                }
            }
            else
            {                                                
                agent.isStopped = false;
				agent.SetDestination(currplayerobj.transform.position); //player not in range; move to the player
                lookatplayer();
            }
        }
        else //if gate exists, make it priority
        {                                    
            if (Vector3.Distance(transform.position, currgate.transform.position) > 2)
            {
                agent.isStopped = false;
                var bump = currgate.transform.position - bastion.transform.position; //so that the object doesn't obscure in the game
                agent.SetDestination(currgate.transform.position + bump * 0.05f);    
                lookatgate();
            }
            else
            {
                if (atkReady)
                {
                    lookatgate();
                    agent.isStopped = true;
                    atkReady = false;
                    Invoke("ReadyAction", myroot.atkSpeed);
                    int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);
                    gateroot.TakeDamege(dmg);     
                }
            }
        }
    }

    public void attack_player_ranged() //this method controls the behaviour of the shoota boyz
    {
        if (currgate == null) //if the gate has fallen
        {
            if (Vector3.Distance(transform.position, currplayerobj.transform.position) <= 13f && //is the player within shooting range
                Vector3.Distance(transform.position, currplayerobj.transform.position) > 2.5f)
            {
                if (atkReady && shootstaggered)
                {
                    agent.Stop();
                    lookatplayer();
                    atkReady = false;
                    Invoke("ReadyAction", myroot.atkSpeed);
                    muzzlelight.Activate();   
                    BulletRay.Emit(1);
                    int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);

                    GameObject missileClone = null; 

                    if (missileClone == null)
                        missileClone = Instantiate(bullet, transform.position, Quaternion.identity);

                    missileClone.GetComponent<Bullet>().damage = dmg;
                    missileClone.GetComponent<Bullet>().target = currplayerobj.GetComponent<Root>();
                   }
            }
            else if (Vector3.Distance(transform.position, currplayerobj.transform.position) <= 2f) //is the player within melee range
            { 
                if (atkReady && shootstaggered)
                {
                    agent.Resume();
                    lookatplayer();
                    atkReady = false;
                    Invoke("ReadyAction", myroot.atkSpeed);
                    int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);
                    currplayerobj.GetComponent<Root>().TakeDamege(dmg);      
                }
            }
            else
            {
                agent.Resume();                                       
                agent.SetDestination(currplayerobj.transform.position); //move to the player if out of range
                lookatplayer();
            }
        }
        else //if the gate is still standing
        {                                   
            if (Vector3.Distance(transform.position, currgate.transform.position) > 8) //go to the gate and attack it
            {
                agent.Resume();
                agent.SetDestination(currgate.transform.position);   
                lookatgate();
            }
            else
            {
                if (atkReady && shootstaggered)
                {
                    lookatgate();
                    agent.Stop();
                    atkReady = false;
                    Invoke("ReadyAction", myroot.atkSpeed);
                    int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);
                    GameObject missileClone = Instantiate(bullet, transform.position,
                        Quaternion.identity);
                    missileClone.GetComponent<Bullet>().damage = dmg;        
                    missileClone.GetComponent<Bullet>().target = gateroot;       
                    muzzlelight.Activate();   
                    BulletRay.Emit(1);
                }
            }
        }
    }

    public void choose_target_intelligent() //this method controls the behaviour of the rokkit boyz and meganobz when choosing their target
    {                              
        if (attack_bastion_bool) //if their target is the bastion
        {
            if (currgate == null) //if the gate has fallen
            {
                if (Vector3.Distance(transform.position, bastion.transform.position + bastion_offset) > 6) //go to bastion
                {
                    agent.Resume();
                    agent.SetDestination(bastion.transform.position + bastion_offset); 
                    lookatbastion();
                }
                else //if at bastion, attack it
                {
                    if (atkReady)
                    {
                        lookatbastion();
                        agent.Stop();
                        atkReady = false;
                        Invoke("ReadyAction", myroot.atkSpeed);
                        int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);
                        var aimdirectioni = bastion.transform.position + new Vector3(0, 4, 0) - (transform.position + new Vector3(0, 2, 0));

                        GameObject missileClone = Instantiate(bullet, transform.position,
                            Quaternion.LookRotation(aimdirectioni) * adjust);
                        missileClone.GetComponent<Bullet>().damage = dmg;           
                        missileClone.GetComponent<Bullet>().target = bastion.GetComponent<Root>();     
                        muzzlelight.Activate(); 
                        BulletRay.Emit(1);
                    }
                }
            }
            else //if the gate is still there
            {
                if (Vector3.Distance(transform.position, currgate.transform.position) > 8) //go to gate, attack it
                {
                    agent.Resume();
                    agent.SetDestination(currgate.transform.position);     
                    lookatgate();
                }
                else
                {
                    lookatgate();
                    if (atkReady)
                    {
                        agent.Stop();
                        atkReady = false;
                        Invoke("ReadyAction", myroot.atkSpeed);
                        int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);
                        var aimdirectioni = currgate.transform.position + new Vector3(0, 5, 0) - (transform.position + new Vector3(0, 2, 0));
                        GameObject missileClone = Instantiate(bullet, transform.position,
                            Quaternion.LookRotation(aimdirectioni) * adjust);
                        missileClone.GetComponent<Bullet>().damage = dmg;  
                        missileClone.GetComponent<Bullet>().target = gateroot;   
                        muzzlelight.Activate();    
                        BulletRay.Emit(1);
                    }
                }
            }
        }
        else if (attack_player_bool) //if its priority is the closest player, move to them
        {
            agent.SetDestination(currplayerobj.transform.position); 
            lookatplayer();
        }
    }

    public void attack_player_intelligent() //this method controls the behaviour of the rokkit boyz and meganobz when targeting a player
    {                                          
        if (Vector3.Distance(transform.position, currplayerobj.transform.position) <= 12.5f && //if in shooting range, attack
            Vector3.Distance(transform.position, currplayerobj.transform.position) > 0.8f)
        {
            if (atkReady)
            {
                lookatplayer();
                agent.Stop();
                in_combat = true;
                atkReady = false;
                Invoke("ReadyAction", myroot.atkSpeed);
                int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);
                var aimdirectioni = currplayerobj.transform.position + 
					new Vector3(0, 1, 0) - (transform.position + new Vector3(0, 2, 0));
                GameObject missileClone = Instantiate(bullet, transform.position,
                    Quaternion.LookRotation(aimdirectioni) * adjust);
                missileClone.GetComponent<Bullet>().damage = dmg;   
                missileClone.GetComponent<Bullet>().target = currplayerobjroot;
                muzzlelight.Activate(); 
                BulletRay.Emit(1);
            }
        }
        else if (Vector3.Distance(transform.position,
          currplayerobj.transform.position) <= 0.8f) //if in melee range, attack
        { 
            if (atkReady)
            {
                lookatplayer();
                agent.Resume();
                in_combat = true;
                atkReady = false;
                Invoke("ReadyAction", myroot.atkSpeed);
                int dmg = myroot.atk + UnityRandom.Range(-myroot.atkVar, myroot.atkVar + 1);
                currplayerobjroot.TakeDamege(dmg);  
            }
        }
        else
        {
            agent.Resume(); //if out of range, restart the target choosing process
            in_combat = false;
            choose_target_intelligent();
        }
    }

    public void suicide_ork_attack() //this method controls the behaviour of the ork bombaz
    {
        agent.isStopped = false;
        find_closest_player();
        if (!suicidetargetpicked) //pick the target for this particular bomba
        {
            var bastionhealthratio = bastion.GetComponent<Root>().HP / 
				GameObject.FindGameObjectWithTag("wave_control_obj").GetComponent<wave_control>().initbastionhealth; 
            if (bastionhealthratio <= 0.25f) //if bastion has less than a quarter health, try to finish it off
            {
                suicidetarg = "bastion";
            }
            else //otherwise, pick the closest/weakest player
            {
                suicidetarg = "player";
            }
            suicidetargetpicked = true;
        }
        foreach (var player in playerobjs)
        {
            if (Vector3.Distance(transform.position, player.transform.position) <= 5.5f) //if it's within range of ANY player at any time, it primes
            {
                suicide_detected = true;
                agent.isStopped = true;
            }
        }
        if (Vector3.Distance(transform.position, bastion.transform.position) <= //if it's within range of the bastion at any time, it primes
			5.5f && suicidetarg == "bastion")
        {
            suicide_detected = true;
            agent.isStopped = true;
        }
        if (suicide_detected)
        {
            agent.isStopped = true;
            StartCoroutine(blow_up(1.2f));
        }
        else
        {
            agent.isStopped = false;
            if (suicidetarg == "bastion") //if in the clear and targeting bastion, move there
            {
                agent.SetDestination(bastion.transform.position);
            }
            else if (suicidetarg == "player") //if in the clear and targeting player, move there
            {
                find_weakest_player();
                agent.SetDestination(currweakestplayer.transform.position);
            }
        }
    }

	public void suicide_explode_method() //this method controls the explosion of the ork bombaz (ensures it only runs once)
    {
        GameObject missileClone = Instantiate(artillerybullet, transform.position, Quaternion.identity);
        int dmg = 100;
        missileClone.GetComponent<ArtBullet>().damage = dmg; //kaboom
        missileClone.GetComponent<ArtBullet>().target = transform.position;
        Destroy(gameObject);
    }

    IEnumerator blow_up(float delay) //this method controls the explosion of the ork bombaz
    {
        if (isCoroutineExecuting)
        {
            yield break;
        }
        isCoroutineExecuting = true;
        yield return new WaitForSeconds(delay); //delay of one second before explosion
        suicide_explode_method();
        isCoroutineExecuting = false;
    }



    ///
    /// POSITIONING AND ROTATION BELOW
    ///



    public void find_closest_player() //this method finds the closest player to the current enemy
    {
		if (playerobjs != null) {
			currplayerobj = playerobjs [0];
			foreach (var player in playerobjs) {
				float dist1 = Vector3.Distance (transform.position, currplayerobj.transform.position);
				float dist2 = Vector3.Distance (transform.position, player.transform.position);
				if (dist1 >= dist2)
					currplayerobj = player;
			}
		}
    }

    public void find_weakest_player() //this method finds the weakest player in the match
    {
        currweakestplayer = playerobjs[0];
        foreach (var player in playerobjs)
        {
            float hp1 = currplayerobj.GetComponent<Root>().HP;
            float hp2 = player.GetComponent<Root>().HP;
            if (hp1 >= hp2)
                currweakestplayer = player;
        }
    }

    public void ReadyAction() //for controlling the rate of fire
    {
        atkReady = true;
    }

    public Vector3 RandomCircle(Vector3 center, float radius) //this method finds a random point at a certain radius around an object
    {
        float ang = UnityRandom.value * 360;
        Vector3 pos;
        pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
        pos.y = center.y;
        pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
        return pos;
    }


    public void randloc() //for buggies and artillery tanks that drift
    {
        wanderlocation = RandomCircle(bastion.transform.position, wanderrad);
    }

    void randbombloc() //for artillery target shells
    {
        droplocation = RandomCircle(bastion.transform.position, 10);
    }

    public void Switch() //controls how frequently the buggies/tanks go from drifting to shooting
    {
        isroaming = !isroaming;
    }

    public void Shootstagger() //staggers ranged shooting
    {
        shootstaggered = !shootstaggered;
    }

    public void lookatgate() //makes the current enemy look at the closest gate
    {
        Quaternion rotationToLookAt = Quaternion.LookRotation(  
            currgate.transform.position - transform.position);
        float rotationY = Mathf.SmoothDampAngle(transform.eulerAngles.y,
            rotationToLookAt.eulerAngles.y, ref velocityRoto,
            rotateSpeedWhenAtk * Time.deltaTime);
        transform.eulerAngles = new Vector3(0, rotationY, 0);
    }

	public void lookatplayer() //makes the current enemy look at the player in question
    {
        Quaternion rotationToLookAt = Quaternion.LookRotation(   
            currplayerobj.transform.position - transform.position);
        float rotationY = Mathf.SmoothDampAngle(transform.eulerAngles.y,
            rotationToLookAt.eulerAngles.y, ref velocityRoto,
            rotateSpeedWhenAtk * Time.deltaTime);
        transform.eulerAngles = new Vector3(0, rotationY, 0);
    }

	public void lookatbastion() //makes the current enemy look at the bastion
    {
        Quaternion rotationToLookAt = Quaternion.LookRotation(
            bastion.transform.position - transform.position);
        float rotationY = Mathf.SmoothDampAngle(transform.eulerAngles.y,
            rotationToLookAt.eulerAngles.y, ref velocityRoto,
            rotateSpeedWhenAtk * Time.deltaTime);
        transform.eulerAngles = new Vector3(0, rotationY, 0);
    }

    public void tokenpopup(bool localKill = false) //UI showing tokens earned from killing the current enemy
    {
        tokenfromkill.GetComponent<Canvas>().worldCamera = Camera.main;
        var texts = tokenfromkill.GetComponentInChildren<Text>();
        texts.text = tokenValue.ToString();

        texts.color = localKill ? Color.red : Color.white;

        var tfk = Instantiate(tokenfromkill);
        tfk.transform.position = lastpos;
        tfk.transform.rotation = Camera.main.transform.rotation;
    }

    public void easteregg() //fill this with beautiful content as you see fit
    {

    }

    private void OnDeath(Root root, string killer) //ouch
    {
        if (root.Armour < Armour.Medium && BloodSplatterPrefab != null && BloodSplatters.Length > 0)
        {
            GameObject go = GameObject.Instantiate<GameObject>(BloodSplatterPrefab, transform.position, transform.rotation);
            go.transform.position = transform.position;
            go.transform.LookAt(go.transform.position + Vector3.up);
            go.GetComponentInChildren<Image>().sprite = BloodSplatters[Random.Range(0, BloodSplatters.Length)];

            RectTransform otherTransform = go.GetComponent<RectTransform>();
            go.transform.localScale = new Vector3((GetComponent<Collider>().bounds.extents.x * 2) / otherTransform.rect.width, (GetComponent<Collider>().bounds.extents.z * 2) / otherTransform.rect.height, 1);
        }
    }

    private void OnReceive(NetworkPacket pack, string id) //received pack!
    {
        if (pack.Header == PacketType.WaveFinishedPack)
            waveOverPack = (WaveOverPack)pack;
        if (pack.Header == PacketType.AIRequestPacket && Network.IsHost)
        {
            AIRequestPacket aiPack = (AIRequestPacket)pack;

            if (aiPack.AIID == this.networkID)
            {
                Network.Send(new AIResponsePack(networkID), id);
            }
        }
        if (pack.Header == PacketType.AIResponsePacket)
        {
            AIResponsePack aiPack = (AIResponsePack)pack;

            if (aiPack.AIID == networkID)
            {
                lastUpdate = System.DateTime.Now;
                hasSentPacket = false;
            }
        }
    }
}