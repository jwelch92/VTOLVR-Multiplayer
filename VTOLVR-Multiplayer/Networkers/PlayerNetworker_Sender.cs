﻿using System.Collections;
using UnityEngine;
using Harmony;

class PlayerNetworker_Sender : MonoBehaviour
{
    public ulong networkUID;
    private Message_Respawn lastMessage;
    public Health health;

    public TempPilotDetacher detacher;
    //public GearAnimator[] gears;
    //public FloatingOriginShifter shifter;
    public EjectionSeat ejection;
    public EngineEffects[] effects;

    Coroutine repspawnTimer;

    Transform target;
    Transform ejectorParent;
    Transform canopyParent;
    Vector3 ejectorSeatPos;
    Quaternion ejectorSeatRot;
    Vector3 canopyPos;
    Quaternion canopyRot;

    GameObject hud;
    GameObject hudWaypoint;

    void Awake()
    {
        lastMessage = new Message_Respawn(networkUID, new Vector3D(), new Quaternion());

        health = GetComponent<Health>();


        if (health == null)
            Debug.LogError("health was null on player " + gameObject.name);
        else
            health.OnDeath.AddListener(Death);

        detacher = GetComponentInChildren<TempPilotDetacher>();
        //gears = GetComponentsInChildren<GearAnimator>();
        //shifter = GetComponentInChildren<FloatingOriginShifter>();
        ejection = GetComponentInChildren<EjectionSeat>();
        ejection.OnEject.AddListener(Eject);

        //ejectorSeatPos = ejection.transform.localPosition;
        //ejectorSeatRot = ejection.transform.localRotation;

        //target = detacher.cameraRig.transform.parent;
        //ejectorParent = ejection.gameObject.transform.parent;
        //if (ejection.canopyObject != null) {
        //    canopyParent = ejection.canopyObject.transform.parent;
        //    canopyPos = ejection.canopyObject.transform.localPosition;
        //    canopyRot = ejection.canopyObject.transform.localRotation;
        //}

        effects = GetComponentsInChildren<EngineEffects>();
    }

    IEnumerator RespawnTimer()
    {
        Debug.Log("Starting respawn timer.");

        yield return new WaitForSeconds(15);

        Debug.Log("Finished respawn timer.");

        ReArmingPoint rearmPoint = GameObject.FindObjectOfType<ReArmingPoint>();

        //UnEject();
        //PutPlayerBackInAircraft();
        //RepairAircraft();

        //foreach (GearAnimator gear in gears) {
        //    gear.ExtendImmediate();
        //}

        //GetComponent<Rigidbody>().velocity = Vector3.zero;
        //transform.position = rearmPoint.transform.position + Vector3.up * 10;
        //transform.rotation = rearmPoint.transform.rotation;

        Destroy(VTOLAPI.GetPlayersVehicleGameObject());
        Destroy(detacher.cameraRig);
        Destroy(detacher.gameObject);
        Destroy(ejection.gameObject);
        Destroy(BlackoutEffect.instance);
        foreach (EngineEffects effect in effects) {
            Destroy(effect);
        }
        //as much stuff as im destroying, some stuff is most likely getting through, future people, look into this
        
        AudioController.instance.ClearAllOpenings();

        GameObject newPlayer = Instantiate(PilotSaveManager.currentVehicle.vehiclePrefab);
        FlightSceneManager.instance.playerActor = newPlayer.GetComponent<Actor>();
        FlightSceneManager.instance.playerActor.flightInfo.PauseGCalculations();
        FlightSceneManager.instance.playerActor.flightInfo.OverrideRecordedAcceleration(Vector3.zero);

        rearmPoint.voiceProfile.PlayMessage(GroundCrewVoiceProfile.GroundCrewMessages.Success);
        PilotSaveManager.currentScenario.totalBudget = 999999;
        PilotSaveManager.currentScenario.initialSpending = 0;
        PilotSaveManager.currentScenario.inFlightSpending = 0;

        rearmPoint.BeginReArm();

        PlayerManager.SetupLocalAircraft(newPlayer, newPlayer.transform.position, newPlayer.transform.rotation, networkUID);

        lastMessage.UID = networkUID;
        
        if (Networker.isHost)
            NetworkSenderThread.Instance.SendPacketAsHostToAllClients(lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliableNoDelay);
        else
            NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliableNoDelay);
        
    }

    void UnEject()
    {
        if (ejection.canopyObject)
        {
            ejection.canopyObject.GetComponentInChildren<Collider>().enabled = false;
            ejection.canopyObject.GetComponentInChildren<Collider>().gameObject.layer = 9;//set to debris layer
        }
        ejection.gameObject.GetComponentInChildren<Collider>().enabled = false;
        ejection.gameObject.GetComponentInChildren<Collider>().gameObject.layer = 9;//set to debris layer

        if (ejection.canopyObject)
        {
            Destroy(ejection.canopyObject.GetComponent<Rigidbody>());
            Destroy(ejection.canopyObject.GetComponent<FloatingOriginTransform>());

            ejection.canopyObject.transform.parent = canopyParent;
            ejection.canopyObject.transform.localPosition = canopyPos;
            ejection.canopyObject.transform.localRotation = canopyRot;
        }

        BlackoutEffect componentInChildren = VRHead.instance.GetComponentInChildren<BlackoutEffect>();
        if (componentInChildren)
        {
            componentInChildren.rb = GetComponent<Rigidbody>();
            componentInChildren.useFlightInfo = true;
        }
        ejection.gameObject.transform.parent = ejectorParent;
        ejection.transform.localPosition = ejectorSeatPos;
        ejection.transform.localRotation = ejectorSeatRot;

        Destroy(ejection.gameObject.GetComponent<FloatingOriginShifter>());
        Destroy(ejection.gameObject.GetComponent<FloatingOriginTransform>());
        ejection.seatRB.isKinematic = true;
        ejection.seatRB.interpolation = RigidbodyInterpolation.None;
        ejection.seatRB.collisionDetectionMode = CollisionDetectionMode.Discrete;

        ModuleParachute parachute = ejection.GetComponentInChildren<ModuleParachute>();
        parachute.CutParachute();
        
        Traverse.Create(ejection).Field("ejected").SetValue(false);//does nothing, cannot eject a seccond time
        //i dont think ejecting is necessary for now, but someone prob ought look into that

        //shifter.enabled = true;
        //AudioController.instance.AddExteriorOpening("eject", 0f);
    }

    void PutPlayerBackInAircraft()
    {
        detacher.cameraRig.transform.parent = target;
        detacher.cameraRig.transform.position = target.position;
        detacher.cameraRig.transform.rotation = target.rotation;

        detacher.pilotModel.SetActive(false);

        Destroy(detacher.cameraRig.GetComponent<FloatingOriginShifter>());
        Destroy(detacher.cameraRig.GetComponent<FloatingOriginTransform>());
        foreach (VRHandController vrhandController2 in VRHandController.controllers)
        {
            if (vrhandController2)
            {
                Destroy(vrhandController2.gameObject.GetComponent<VRTeleporter>());
            }
        }

        //shifter.enabled = true;
    }

    void RepairAircraft()
    {
        FlightAssist flightAssist = GetComponentInChildren<FlightAssist>();
        if (flightAssist != null)
        {
            flightAssist.assistEnabled = true;
        }
        else {
            Debug.Log("Could not fix flight assists");
        }

        RCSController rcsController = GetComponentInChildren<RCSController>();
        if (rcsController != null)
        {
            Traverse.Create(rcsController).Field("alive").SetValue(true);
        }
        else
        {
            Debug.Log("Could not fix rcs controller");
        }

        Battery battery = GetComponentInChildren<Battery>();
        if (battery != null)
        {
            Traverse.Create(battery).Field("isAlive").SetValue(true);
            battery.Connect();
        }
        else
        {
            Debug.Log("Could not fix battery");
        }

        GameObject hud = GameObject.Find("CollimatedHud");
        if (hud != null)
        {
            hud.SetActive(true);
        }
        else
        {
            Debug.Log("Could not fix hud");
        }

        GameObject hudWaypoint = GameObject.Find("WaypointLead");
        if (hudWaypoint != null)
        {
            hudWaypoint.SetActive(true);
        }
        else
        {
            Debug.Log("Could not fix hudWaypoint");
        }

        VRJoystick joystick = GetComponentInChildren<VRJoystick>();
        if (joystick != null)
        {
            joystick.sendEvents = true;
        }
        else
        {
            Debug.Log("Could not fix joystick");
        }

        VRInteractable[] levers = GetComponentsInChildren<VRInteractable>();
        foreach (VRInteractable lever in levers)
        {
            lever.enabled = true;
        }
        Debug.Log("Fixed " + levers.Length + " levers");
    }

    void Eject()
    {
        health.Kill();
    }

    void Death()
    {
        repspawnTimer = StartCoroutine("RespawnTimer");
    }
}
