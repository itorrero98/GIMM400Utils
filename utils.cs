/* *******************LINK TO MY COLLAB NOTEBOOK********************* */
// https://colab.research.google.com/drive/1Pl-OaahFLTa5l_-4WfDnS60w31Izh5Ep

/*
    Helper function used for our racing system.
    Our checkpoints are made of multiple posts, so we have
    to go through each object and switch it's material.
    We then remove it from our list of checkpoints and
    reset the current checkpoint
*/
public void NextCheckpoint(GameObject toRemove)
{
    m_checkpointCount++;
    foreach (Transform child in toRemove.transform.parent.transform)
    {
        if (child.GetComponent<Renderer>())
        {
            child.GetComponent<Renderer>().material = m_Passed;
        }
    }
    m_CheckPoints.Remove(toRemove);
    Destroy(toRemove);
    m_CurrCheckpoint = m_CheckPoints[0];

    foreach (Transform currChild in m_CurrCheckpoint.transform.parent.transform)
    {
        if (currChild.GetComponent<Renderer>())
        {
            currChild.GetComponent<Renderer>().material = m_Current;
        }
    }
}

/* 
    Simple singleton setup for game manager. This concept
    Is similar to how you would check if it is a local player
    and if it isn't then you could return and or destroy the object
*/
void Start()
{
    m_checkpointCount = 0;
    m_FinishedDetails.enabled = false;
    m_FinishedTime.enabled = false;
    if (m_Instance == null)
    {
        m_Instance = this;

    }
    else if (m_Instance != this)
    {
        Destroy(gameObject);
    }
    m_CheckPoints = GameObject.FindGameObjectsWithTag("checkpoint").ToList();
    m_CurrCheckpoint = m_CheckPoints[0];
    m_TotalCheckPoints = m_CheckPoints.Count();
}

/*
    This function does a few things. It captures the intent to connect
    It also switches the UI elements to indicate to the player they are
    connecting to the server
    We also take the players name that the user typed in elsewhere to
    identify them
    It then utilizes photon to join a lobby that exists on the current server
*/
public void Connect()
{
    // keep track of the will to join a room, because when we come back from the game we will get a callback that we are connected, so we need to know what to do then
    isConnecting = true;

    progressLabel.SetActive(true);
    controlPanel.SetActive(false);
    // we check if we are connected or not, we join if we are , else we initiate the connection to the server.
    if (PhotonNetwork.IsConnected)
    {
        // #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnJoinRandomFailed() and we'll create one.
        PhotonNetwork.JoinRandomRoom();
    }
    else
    {
        // #Critical, we must first and foremost connect to Photon Online Server.
        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.ConnectUsingSettings();
    }
}
/*
    Photon makes it really easy to sync our scene accross instances
    The master client will be the one that everyone else syncs their game with
*/
void Awake()
{
    // #Critical
    // this makes sure we can use PhotonNetwork.LoadLevel() on the master client and all clients in the same room sync their level automatically
    PhotonNetwork.AutomaticallySyncScene = true;
}
/*
    This function doesn't actually do anything useful
    This is how you save, and access a local value
    GetInt, can be overloaded with a fallback value 
    in case you try and access the value and it doesn't exist
*/
void SimpleLocalStorage()
{
    PlayerPrefs.SetInt("myPref", 5);
    myint = PlayerPrefs.GetInt("myInt");
    myint = PlayerPrefs.GetInt("myInt", 5);
}
/*
    This sets up the values that our ML-Agent is going to be
    observing. We techincally have 10 observation values here
    x,y and z of target.position && this.transform.position
    and values of rbody velocity and angularvelocity
*/
public override void CollectObservations()
{
    // Target and Agent positions
    AddVectorObs(Target.position);
    AddVectorObs(this.transform.position);

    // Agent velocity
    AddVectorObs(rBody.velocity.x);
    AddVectorObs(rBody.velocity.z);
    AddVectorObs(rBody.angularVelocity.x);
    AddVectorObs(rBody.angularVelocity.y);
}
/*
    Handles "death" or falling off the map for the ml agent
    We reset it on the platform, reset it's velocities and
    reset the target position
    If we are only resetting them (not because they fell off) then we
    simply reset the target
*/
public override void AgentReset()
{
    if (this.transform.position.y < 0)
    {
        // If the Agent fell, zero its momentum
        this.rBody.angularVelocity = Vector3.zero;
        this.rBody.velocity = Vector3.zero;
        this.transform.position = new Vector3(0, 0.5f, 0);
    }

    // Move the target to a new spot
    Target.position = new Vector3(Random.value * 8 - 4,
                                  0.5f,
                                  Random.value * 8 - 4);
}
/*
    This is where all the actual controls of the ML-Agent occurs
    We essentially tell the agent to move in a random direction
    we then apply a force given the values it chose
    We then calculate the distance to the desired target
    if we are within a threshold we give the ML-Agent a reward
    this will reinforce that the ML-Agent is trying to get close
    to the target. We give it a command of "done" which will start
    it over again and it will try to do it again
    If it falls off, we give it a negative reward, this is so the 
    ML-Agent trys to avoid the edges of the platform. It will
    eventually learn that falling is bad and will avoid it when possible
*/
public override void AgentAction(float[] vectorAction, string textAction)
{
    // Actions, size = 2
    Vector3 controlSignal = Vector3.zero;
    controlSignal.x = vectorAction[0];
    controlSignal.z = vectorAction[1];
    rBody.AddForce(controlSignal * m_Force);

    // Rewards
    float distanceToTarget = Vector3.Distance(this.transform.position,
                                              Target.position);

    // Reached target
    if (distanceToTarget < 1.42f)
    {
        SetReward(1.0f);
        Done();
    }

    // Fell off platform
    if (this.transform.position.y < 0)
    {
        SetReward(-.3f);
        Done();
    }
}

/*
Grab the next navpoint in our list of nav points
*/
public Transform GetNextNavPoint()
{
    navPointNum = (navPointNum + 1) % navPoints.Length;
    return navPoints[navPointNum].transform;
}
/*
    Used to force the state machine to choose a random location and travel to it
*/
public Vector3 GetRandomDest()
{
    return new Vector3(Random.Range(0, randDestRange), 0f, Random.Range(0, randDestRange));
}
/*
    Lets the agent create a new nav point that it can
    navigate to in patrol state
*/
public void AddNewNavPoint()
{
    navPoints = null;
    GameObject np = Instantiate(newPatrolPoint, gameObject.transform);
    np.transform.parent = null;
    navPoints = GameObject.FindGameObjectsWithTag("navpoint");
}
/*
    Used for the multiply state
    Creates an entire new agent
*/
public void CreateNewAgent()
{
    Debug.Log("Creating new agent");
    GameObject newAgent = Instantiate(gameObject, gameObject.transform);
    newAgent.transform.parent = null;
    Agents.Add(newAgent);
    navPoints = GameObject.FindGameObjectsWithTag("navpoint");
}
/*
    Changes the agents color to indicate it's current state
*/
public void ChangeColor(Color color)
{
    foreach (Renderer r in childrenRend)
    {
        foreach (Material m in r.materials)
        {
            m.color = color;
        }
    }
}
/*
    Check if player is in range, if it is then chase them
*/
public bool CheckIfInRange(string tag)
{
    enemies = GameObject.FindGameObjectsWithTag(tag);
    if (enemies != null)
    {
        foreach (GameObject g in enemies)
        {
            if (Vector3.Distance(g.transform.position, transform.position) < detectionRange)
            {
                enemyToChase = g;
                return true;
            }
        }
    }
    return false;
}
void Start()
{
    navPoints = GameObject.FindGameObjectsWithTag("navpoint");
    ai = GetComponent<UnityStandardAssets.Characters.ThirdPerson.AICharacterControl>();
    childrenRend = GetComponentsInChildren<Renderer>();
    SetState(new PatrolState(this));
    Agents.Add(gameObject);
}
/*
    This is what maintains our state machine
    It will constantly check if the agent needs to 
    transition and also cause it to perform wahtever
    it's "act" function entails
*/
void Update()
{
    currentState.CheckTransitions();
    currentState.Act();
}

//Used to switch states
public void SetState(State state)
{
    if (currentState != null)
    {
        currentState.OnStateExit();
    }

    currentState = state;
    gameObject.name = "AI agent in state " + state.GetType().Name;

    if (currentState != null)
    {
        currentState.OnStateEnter();
    }
}
/*
    Example functinos for a state machine
    Checktransitions checks to see if the agent needs to
    switch to a new state
    act performs the actions that relate to this state in particular
    onStateEnter is essentially an init function for this state
*/
public override void CheckTransitions()
{
    if (stateController.CheckIfInRange("Player"))
    {
        stateController.SetState(new ChaseState(stateController));
    }
    if (navPointsHit >= stateController.navPoints.Length)
    {
        stateController.SetState(new MoveRandomlyState(stateController));
    }
}
public override void Act()
{
    if (destination == null || stateController.ai.DestinationReached())
    {
        navPointsHit++;
        destination = stateController.GetNextNavPoint();
        stateController.ai.SetTarget(destination);
    }
}
public override void OnStateEnter()
{
    navPointsHit = 0;
    destination = stateController.GetNextNavPoint();
    if (stateController.ai.agent != null)
    {
        stateController.ai.agent.speed = 1f;
    }
    stateController.ai.SetTarget(destination);
    stateController.ChangeColor(Color.blue);
}
