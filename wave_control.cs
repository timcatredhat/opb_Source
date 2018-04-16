/// /// <summary>
/// THIS SCRIPT CONTROLS THE SPAWNING OF ENEMY WAVES, THEIR TIMING, AND THEIR DISTRIBUTION
/// </summary>

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class wave_control : MonoBehaviour
{
    private static wave_control instance;
    public static wave_control Instance { get { return instance; } }

    public GameObject ork_melee_basic;
    public GameObject ork_ranged_basic;
    public GameObject ork_ranged_intelligent; 
    public GameObject ork_tank;
    public GameObject ork_boss_1; //a meganob
    public GameObject ork_artillery;
    public GameObject ork_bomba;
    public GameObject exploding_barrel;

    public List<string> wavenames;	
    public GameObject[] spawnhubs, barrelspawns; //spawn points for enemies and barrels
    public GameObject sh1, sh2, sh3, sh4; //the four individual spawn points
    public List<GameObject> currentspawnpts; //active spawn points
    public int randhub; //an integer that randomly picks which spawn point to use
    public Queue<GameObject> toReactivate;

    public float pausetime;
    public Vector3 spawnpos; //Where the thing spawns on the map
    public int randbarrel;
    public GameObject[] orksonscreen; //orks in current wave
    public int numorks; //when zero, call next wave
	public int keepaddingme;

    public int wavenum; //what wave we're on
    public Text waveindicator; //UI for wave number
    public CanvasGroup wavebreakindicator, waveindicatorobj;
    public float wavetime, nextwave_time, breaktime; //how long each wave lasts
    public GameObject[] players; //all players
    public int playernum; //how many players are playing
    public Image wavebar;
    float rate_orks;

    public int howmanytanks, howmanymelee, howmanyranged, howmanysmart, howmanyartillery, howmanysuicide; //for looping in the functions
    public int howmanyinwave; //total number of orks in wave
    public bool isCoroutineExecuting, iswaghexecuting, isinsultexecuting, saidwagh, saidinsult; //controls the audio timing

    public float initbastionhealth, initplayerhealth;
    public AudioSource[] waveaudios;

    public bool spawnflash, switchtext=false, breakover = false;
    private Queue<BarrelPositionPack> barrelPositions;
    private object barrelLock = new object();

    private System.DateTime lastCheck;
    private int numorksLastCheck;
    private WaveStartPack waveStartPack = null;

    void Start()
    {
        instance = this;
        Network.OnReceive += OnReceive;
        barrelPositions = new Queue<BarrelPositionPack>();
        lastCheck = System.DateTime.Now;

        toReactivate = new Queue<GameObject>();
        initbastionhealth = GameObject.FindGameObjectWithTag("bastion").GetComponent<Root>().HP;
        barrelspawns = GameObject.FindGameObjectsWithTag("barrel_spawn_point"); //barrel spawn points
		sh1 = GameObject.FindGameObjectWithTag("sh1"); //the four spawn points below
		sh2 = GameObject.FindGameObjectWithTag ("sh2");
		sh3 = GameObject.FindGameObjectWithTag ("sh3");
		sh4 = GameObject.FindGameObjectWithTag ("sh4");

        if (Network.IsHost)
        {
            for (int i = 0; i < 4; i++) //place 4 barrels randomly on map
            {
                randbarrel = Random.Range(0, barrelspawns.Length);
                var barrelpos = RandomCircle(barrelspawns[randbarrel].transform.position, 3);
                var newObject = (GameObject)Instantiate(exploding_barrel, barrelpos,
                    Quaternion.identity);
                newObject.tag = "barrel";
                barrelspawns[randbarrel].tag = "Untagged";
                barrelspawns = GameObject.FindGameObjectsWithTag("barrel_spawn_point");
                Network.Send(new BarrelPositionPack(barrelpos, 0));
            }
        }

        players = GameObject.FindGameObjectsWithTag("Player");
        initplayerhealth = players[0].GetComponent<Root>().HP;
        playernum = 1;
        orksonscreen = GameObject.FindGameObjectsWithTag("Ork");
        numorks = orksonscreen.Length;
        breaktime = 12;
        wavenum = 0; howmanymelee = 0; howmanyranged = 0; howmanysmart = 0; howmanytanks = 0; howmanyartillery = 0; //initialize at zeroes
        waveindicator = GameObject.FindGameObjectWithTag("wavetext").GetComponent<Text>(); //UI
        waveindicatorobj = GameObject.FindGameObjectWithTag("wavetext").GetComponent<CanvasGroup>(); //UI
        wavebreakindicator = GameObject.FindGameObjectWithTag("wavebreak_text").GetComponent<CanvasGroup>(); //UI
        wavebar = GameObject.FindGameObjectWithTag("wavebar").GetComponent<Image>(); //UI
        wavebreakindicator.alpha = 0;
        wavetime = Mathf.Pow(wavenum, 0.3f) * 60;
        nextwave_time = Time.time;	

		//wave names below
        wavenames.Add("Kill the Scouts"); wavenames.Add("Defend the Gate"); wavenames.Add("Fury on Wheels"); wavenames.Add("Honour the Emprah");
        wavenames.Add("Behead the Nobz"); wavenames.Add("Melee Overload"); wavenames.Add("Filthy Greenskins"); wavenames.Add("Tyres Burning");
        wavenames.Add("Calm Before the Storm"); wavenames.Add("Artillery Bombz"); wavenames.Add("Ork Flood"); wavenames.Add("Crimson Tide");
        wavenames.Add("Blood Moon"); wavenames.Add("Death in the Dark"); wavenames.Add("Anger of the Gods"); wavenames.Add("Hand to Claw Combat"); wavenames.Add("Poison");
        wavenames.Add("Pursuit"); wavenames.Add("The Darkest Hour"); wavenames.Add("Glimmer of Sunshine");
        print(wavenames);
        waveindicator.text = wavenames[0];

        waveaudios = GetComponents<AudioSource>();

        spawnflash = true;
        InvokeRepeating("Switch", 0, 0.74f);
		InvokeRepeating ("Switch2", 0, 2);
        GetComponent<Root>().OnDeath += OnDeath;
    }

    void OnDeath(Root root, string id)
    {
        Network.OnReceive -= OnReceive;
    }

    void Update()
    {
        while (toReactivate.Count > 0)
            toReactivate.Dequeue().SetActive(true);

        if (waveStartPack != null)
        {
            waveStartPack = null;
            waveaction();
        }

        if (barrelPositions.Count > 0)
        {
            lock(barrelLock)
            {
                while(barrelPositions.Count > 0)
                {
                    Vector3 position = barrelPositions.Dequeue().position;

                    var newObject = (GameObject)Instantiate(exploding_barrel, position,
                    Quaternion.identity);

                    newObject.transform.position = position;
                    newObject.tag = "barrel";
                }
            }
        }

		if (wavenum == 21) { //you win if wave 20 is cleared
			SceneManager.LoadScene ("win");
		}

        if (howmanyinwave != 0) //fill up the wave bar with how many enemies are to be cleared
        {
            rate_orks = (float)numorks / howmanyinwave;
            wavebar.fillAmount = Mathf.Lerp(wavebar.fillAmount, (float)numorks / howmanyinwave, Time.deltaTime * 2f);
        }
        orksonscreen = GameObject.FindGameObjectsWithTag("Ork");
        numorks = orksonscreen.Length;                                              //HOW MANY ENEMIES CURRENTLY IN GAME

        if (Network.IsHost)
        {
            if (numorks > 0 && numorks < howmanyinwave && (System.DateTime.Now - lastCheck).TotalSeconds >= 30)
            {
                if (numorks == numorksLastCheck)
                {
                    foreach (var obj in orksonscreen)
                    {
                        Root rt = obj.GetComponent<Root>();

                        rt.TakeDamege(2 * (int)rt.HP);
                    }

                    numorks = 0;
                }
                else
                {
                    lastCheck = System.DateTime.Now;
                    numorksLastCheck = howmanyinwave;
                }
            }
        }

        players = GameObject.FindGameObjectsWithTag("Player");
        playernum = players.Length;

        if (wavenum < 2) //only one spawn point active
        {
            currentspawnpts.Add(sh1); sh1.SetActive(true); sh2.SetActive(false); sh3.SetActive(false); sh4.SetActive(false);


			if (spawnflash)
            {
                sh1.GetComponent<CanvasGroup>().alpha = 1;
            }
            else
            {
                sh1.GetComponent<CanvasGroup>().alpha -= 0.1f;
            }
        }
        else if (wavenum > 1 && wavenum < 7) //only two spawn points active
        {
            currentspawnpts.Add(sh2); sh1.SetActive(true); sh2.SetActive(true); sh3.SetActive(false); sh4.SetActive(false);
            if (spawnflash)
            {
                sh1.GetComponent<CanvasGroup>().alpha = 1;
                sh2.GetComponent<CanvasGroup>().alpha = 1;
            }
            else
            {
                sh1.GetComponent<CanvasGroup>().alpha -= 0.1f;
                sh2.GetComponent<CanvasGroup>().alpha -= 0.1f;
            }
        }
        else if (wavenum > 6 && wavenum < 11) //three spawn points active
        {
            currentspawnpts.Add(sh3); sh1.SetActive(true); sh2.SetActive(true); sh3.SetActive(true); sh4.SetActive(false);
            if (spawnflash)
            {
                sh1.GetComponent<CanvasGroup>().alpha = 1;
                sh2.GetComponent<CanvasGroup>().alpha = 1;
                sh3.GetComponent<CanvasGroup>().alpha = 1;
            }
            else
            {
                sh1.GetComponent<CanvasGroup>().alpha -= 0.1f;
                sh2.GetComponent<CanvasGroup>().alpha -= 0.1f;
                sh3.GetComponent<CanvasGroup>().alpha -= 0.1f;
            }
        }
        else //all four spawn points active
        {
            currentspawnpts.Add(sh4); sh1.SetActive(true); sh2.SetActive(true); sh3.SetActive(true); sh4.SetActive(true);
            if (spawnflash)
            {
                sh1.GetComponent<CanvasGroup>().alpha = 1;
                sh2.GetComponent<CanvasGroup>().alpha = 1;
                sh3.GetComponent<CanvasGroup>().alpha = 1;
                sh4.GetComponent<CanvasGroup>().alpha = 1;
            }
            else
            {
                sh1.GetComponent<CanvasGroup>().alpha -= 0.1f;
                sh2.GetComponent<CanvasGroup>().alpha -= 0.1f;
                sh3.GetComponent<CanvasGroup>().alpha -= 0.1f;
                sh4.GetComponent<CanvasGroup>().alpha -= 0.1f;
            }
        }


		if (numorks < 1) { //you've cleared the wave
			wavebreak ();
			keepaddingme += 1;
			wave_finished ();
			breakover = true;

			if(!isCoroutineExecuting && wavenum < 21)
			    StartCoroutine (MyDelayMethod (12f)); //THIS IS HOW LONG THE BREAK LASTS BETWEEN WAVES
		} else {
			keepaddingme = 0;
		}


        if (wavenum > 1 && wavenum % 2 != 0 && wavenum % 5 != 0 && !saidwagh) //orks yell wagh
        {
            StartCoroutine(wagh(8f));
        }

        if (wavenum > 3 && wavenum % 5 != 0 && !saidinsult) //orks yell insult
        {
            float randtime = Random.Range(14, 24);
            StartCoroutine(insult(randtime));
        }
        else if (wavenum == 2 && !saidinsult) //orks yell insult
        {
            StartCoroutine(insult(4f));
        }
    }

	public void wave_finished(){
		var temp = GameObject.FindObjectsOfType<PlayerControl>();
		foreach (PlayerControl control in temp) {
			if(keepaddingme==1&&wavenum>0)
			control.wave_finished_heal ();
		}
	}

    void waveaction()
    {
        Enemy_parent_script[] objs = GameObject.FindObjectsOfType<Enemy_parent_script>();

        foreach (Enemy_parent_script obj in objs)
            Destroy(obj.gameObject);

        saidwagh = false;
        saidinsult = false;
        wavebreakindicator.alpha = 0;
        waveindicatorobj.alpha = 1;
        wavenum += 1;
        waveaudios[wavenum - 1].Play();
        if (wavenum > 1)
        {
            waveaudios[wavenum - 2].volume = 0;
        }
		//call the enemies below
        call_tank();
        call_ork_smart();
        call_ork_ranged();
        call_ork_melee();
        call_boss_baby();
        call_artillery();
        call_suicide();
        call_lull_wave();
        waveindicator.text = "Wave " + wavenum.ToString() + ": " + wavenames[wavenum - 1]; //UI
        howmanyinwave = howmanymelee + howmanyranged + howmanytanks + howmanysmart + howmanysuicide;
    }

    void wavebreak()
    {
		if (switchtext) {
			wavebreakindicator.alpha += 0.104f; //UI 

		} else {
			wavebreakindicator.alpha -= 0.04f; //UI

		}
		waveindicatorobj.alpha = 0;
    }

    void call_tank() //this method is for spawning rokkit buggies
    { 
		if (wavenum % 5 != 0 && wavenum < 21)
        {
            howmanytanks = 0;
            if (wavenum < 9 && wavenum % 3 == 0)
            {
                howmanytanks = Mathf.RoundToInt(1 * Mathf.Pow(playernum, 0.2f)); //the number of buggies to spawn
            }
            else if (wavenum > 8 && wavenum % 3 == 0)
            {
                howmanytanks = Mathf.RoundToInt(wavenum / 3 * Mathf.Pow(playernum, 0.2f)) - 1;
            }

            for (int i = 0; i < howmanytanks * (int)GameManager.Difficulty; ++i)
            {
                randhub = Random.Range(0, currentspawnpts.Count);
                spawnpos = currentspawnpts[randhub].transform.position +
                new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));

                GameObject newObject = null;

                if (newObject == null)
                    newObject = (GameObject)Instantiate(ork_tank, spawnpos, ork_tank.transform.rotation);

                newObject.tag = "Ork";
            }
        }
    }

    void call_ork_melee() //this method is for spawning slugga boyz
    {
		if (wavenum % 5 != 0 && (wavenum % 5 != 1 || wavenum < 2) && wavenum < 21)
        {
            howmanymelee = 0;
            howmanymelee = Mathf.RoundToInt(3 * Mathf.Pow(1.1f, wavenum - 1) * Mathf.Pow(playernum, 0.2f)); //number of melee to spawn
            for (int i = 0; i < howmanymelee * (int)GameManager.Difficulty; ++i)
            {
                randhub = Random.Range(0, currentspawnpts.Count);

                spawnpos = currentspawnpts[randhub].transform.position +
                new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));

                GameObject newObject = null;

                if (newObject == null)
                    newObject = (GameObject)Instantiate(ork_melee_basic, spawnpos, ork_melee_basic.transform.rotation);

                newObject.tag = "Ork";
            }
        }
		else if (wavenum % 5 == 0 && wavenum < 21) {
			for (int i2 = 0; i2 < (wavenum + 2) * (int)GameManager.Difficulty; ++i2)
			{
				randhub = Random.Range(0, currentspawnpts.Count);

				spawnpos = currentspawnpts[randhub].transform.position +
					new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));

				GameObject newObject = null;

				if (newObject == null)
					newObject = (GameObject)Instantiate(ork_melee_basic, spawnpos, ork_melee_basic.transform.rotation);

				newObject.tag = "Ork";
			}
		}
    }

    void call_ork_ranged() //this method is for spawning shoota boyz
    {   
		if (wavenum % 5 != 0 && (wavenum % 5 != 1 || wavenum < 2) && wavenum < 21)
        {
            howmanyranged = 0;
            howmanyranged = Mathf.RoundToInt(5 * Mathf.Pow(1.08f, wavenum - 2) * Mathf.Pow(playernum, 0.2f)); //number of ranged to spawn
            for (int i = 0; i < howmanyranged * (int)GameManager.Difficulty; ++i)
            {
                randhub = Random.Range(0, currentspawnpts.Count);

                spawnpos = currentspawnpts[randhub].transform.position +
                new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));
                GameObject newObject = null;

                if (newObject == null)
                    newObject = (GameObject)Instantiate(ork_ranged_basic, spawnpos, ork_ranged_basic.transform.rotation);

                newObject.tag = "Ork";
            }
        }
    }

    void call_ork_smart() //this method is for spawning rokkit boyz
    { 
		if (wavenum % 5 != 0 && (wavenum % 5 != 1 || wavenum < 2) && wavenum < 21)
        {
            howmanysmart = 0;
            howmanysmart = Mathf.RoundToInt(5 * Mathf.Pow(1.09f, wavenum - 3) * Mathf.Pow(playernum, 0.2f)); //number of rokkit boyz to spawn
            for (int i = 0; i < howmanysmart * (int)GameManager.Difficulty; ++i)
            {
                randhub = Random.Range(0, currentspawnpts.Count);

                spawnpos = currentspawnpts[randhub].transform.position +
                new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));

                GameObject newObject = null;

                if (newObject == null)
                    newObject = (GameObject)Instantiate(ork_ranged_intelligent, spawnpos, ork_ranged_intelligent.transform.rotation);

                newObject.tag = "Ork";
            }
        }
    }

    void call_boss_baby() //this method is for spawning meganobz
    {
		if (wavenum % 5 == 0 && wavenum < 21)
        {
            for (int i = 0; i < (wavenum / 5 + 1) * (int)GameManager.Difficulty; ++i) //number of meganobz to spawn
            {
                randhub = Random.Range(0, currentspawnpts.Count);

                spawnpos = currentspawnpts[randhub].transform.position +
                    new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));

                GameObject newObject = null;

                if (newObject == null)
                    newObject = (GameObject)Instantiate(ork_boss_1, spawnpos, ork_ranged_intelligent.transform.rotation);

                newObject.tag = "Ork";
            }
        }
    }

    void call_artillery() //this method is for spawning artillery tanks
    {
		if (wavenum >= 10 && wavenum % 2 == 0 && wavenum < 21)
        {
			howmanyartillery = Mathf.RoundToInt(1 + (wavenum - 10) / 8f); //number of tanks to spawn
            for (int i = 0; i < howmanyartillery * (int)GameManager.Difficulty; ++i) 
            {
                randhub = Random.Range(0, currentspawnpts.Count);

				spawnpos = currentspawnpts [randhub].transform.position +
                    new Vector3(Random.Range(-0.7f, 0.7f), 0, Random.Range(-0.7f, 0.7f));

                GameObject newObject = null; 

				if (newObject == null) {
					newObject = (GameObject)Instantiate(ork_artillery, spawnpos, Quaternion.identity);
					newObject.transform.position = spawnpos;
					newObject.transform.rotation = Quaternion.identity;
				}

                newObject.tag = "Ork";
            }
        }
    }

    void call_suicide() //this method is for spawning ork bombaz
    {
		if (wavenum >= 3 && wavenum % 3 == 0 && wavenum < 21)
        {
            howmanysuicide = Mathf.RoundToInt(wavenum / 3); //number of bombaz to spawn
            for (int i = 0; i < howmanysuicide * (int)GameManager.Difficulty; ++i)
            {
                randhub = Random.Range(0, currentspawnpts.Count);

                spawnpos = currentspawnpts[0].transform.position +
                    new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));

                GameObject newObject = null;

                if (newObject == null)
                    newObject = (GameObject)Instantiate(ork_bomba, spawnpos,
                    ork_tank.transform.rotation);

                newObject.tag = "Ork";
            }
        }
    }

    void call_lull_wave() //this method is for floods for orks
    {
		if (wavenum % 5 == 1 && wavenum > 2 && wavenum < 21)
        {
            for (int i = 0; i < (wavenum * 4) * (int)GameManager.Difficulty; ++i) //number of sluggas to spawn
            {
                randhub = Random.Range(0, currentspawnpts.Count);

                spawnpos = currentspawnpts[randhub].transform.position +
                    new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));

                GameObject newObject = null;

                if (newObject == null)
                    newObject = (GameObject)Instantiate(ork_melee_basic, spawnpos, ork_melee_basic.transform.rotation);

                newObject.tag = "Ork";
            }
            for (int i = 0; i < (wavenum * 2) * (int)GameManager.Difficulty; ++i) //number of shootas to spawn
            {
                randhub = Random.Range(0, currentspawnpts.Count);

                spawnpos = currentspawnpts[randhub].transform.position +
                    new Vector3(Random.Range(0f, 2f), 0, Random.Range(0f, 2f));

                GameObject newObject = null;

                if (newObject == null)
                    newObject = (GameObject)Instantiate(ork_ranged_basic, spawnpos, ork_ranged_basic.transform.rotation);

                newObject.tag = "Ork";
            }
        }
    }

	public Vector3 RandomCircle(Vector3 center, float radius) //this method finds a random point at a certain radius around an object
    {
        float ang = Random.value * 360;
        Vector3 pos;
        pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
        pos.y = center.y;
        pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
        return pos;
    }

    IEnumerator MyDelayMethod(float delay)
    {
        if (isCoroutineExecuting)
        {
            yield break;
        }

        isCoroutineExecuting = true;
        Network.Send(new WaveOverPack());

        yield return new WaitForSeconds(delay);

        if (Network.IsHost)
        {
            Network.Send(new WaveStartPack());
            waveaction();
        }
        isCoroutineExecuting = false;
    }

    IEnumerator wagh(float delay)
    {
        if (iswaghexecuting)
        {
            yield break;
        }

        iswaghexecuting = true;

        yield return new WaitForSeconds(delay);

        waveaudios[20].Play();
        waveaudios[20].loop = false;
        saidwagh = true;
        iswaghexecuting = false;
    }

    IEnumerator insult(float delay)
    {
        if (isinsultexecuting)
        {
            yield break;
        }

        isinsultexecuting = true;

        yield return new WaitForSeconds(delay);
        int insultnum = Random.Range(21, 24);
        waveaudios[insultnum].Play();
        waveaudios[insultnum].loop = false;
        saidinsult = true;
        isinsultexecuting = false;
    }

    public void Switch() //For UI flashing effects
    {
        spawnflash = !spawnflash;
    }

	public void Switch2() { //For UI flashing effects
		switchtext = !switchtext;
	}

    private void OnReceive(NetworkPacket pack, string id)
    {        
        switch(pack.Header)
        {
            case PacketType.BarrelPositionPack:
                lock (barrelLock)
                    barrelPositions.Enqueue(pack as BarrelPositionPack);
                break;
            case PacketType.WaveSpawnPack:
                this.waveStartPack = (WaveStartPack)pack;
                break;
        }
    }
}