﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Harmony;

class MissileAuthorityNetworker_Reciever : MonoBehaviour
{
    public ulong networkUID;
    public ulong ownerUID;//0 is host owns the missile
    public bool currentLocalAuthority;

    private Missile thisMissile;
    float originalProxFuse;
    private Traverse traverse2;

    public MissileNetworker_Sender missileSender;
    public MissileNetworker_Receiver missileReceiver;
    public RigidbodyNetworker_Sender rbSender;
    public RigidbodyNetworker_Receiver rbReceiver;

    private Message_MissileChangeAuthority lastChangeMessage;

    private void Start()
    {
        Networker.RequestNetworkUID += RequestUID;

        thisMissile = GetComponent<Missile>();
        originalProxFuse = thisMissile.proxyDetonateRange;

        if (thisMissile.guidanceMode == Missile.GuidanceModes.Heat)
        {
            traverse2 = Traverse.Create(thisMissile.heatSeeker);
        }

        Networker.MissileChangeAuthority += MissileChangeAuthority;

        thisMissile.explodeRadius *= Multiplayer._instance.missileRadius; thisMissile.explodeDamage *= Multiplayer._instance.missileDamage;
    }

    public void MissileChangeAuthority(Packet packet)
    {
        lastChangeMessage = ((PacketSingle)packet).message as Message_MissileChangeAuthority;
        if (lastChangeMessage.networkUID != networkUID)
            return;

        Debug.Log("Missile changing authority!");
        bool localAuthority;
        if (lastChangeMessage.newOwnerUID == 0)
        {
            Debug.Log("The host is now incharge of this missile.");
            if (Networker.isHost)
            {
                Debug.Log("We are the host! This is our missile!");
                localAuthority = true;
            }
            else
            {
                Debug.Log("We are not the host. This is not our missile.");
                localAuthority = false;
            }
        }
        else
        {
            Debug.Log("A client is now incharge of this missile.");
            if (PlayerManager.localUID == lastChangeMessage.newOwnerUID)
            {
                Debug.Log("We are that client! This is our missile!");
                localAuthority = true;
            }
            else
            {
                Debug.Log("We are not that client. This is not our missile.");
                localAuthority = false;
            }
        }

        ChangeAuthority(localAuthority);
    }

    void ChangeAuthority(bool targetLocalAuthority) {
        if (currentLocalAuthority != targetLocalAuthority)
        {
            if (targetLocalAuthority)
            {
                Debug.Log("We should be incharge of this missile");
                Destroy(missileReceiver);
                Destroy(rbReceiver);

                Rigidbody rb = GetComponent<Rigidbody>();
                rb.isKinematic = false;
                thisMissile.proxyDetonateRange = originalProxFuse;

                missileSender = gameObject.AddComponent<MissileNetworker_Sender>();
                if (!thisMissile.hasTarget)
                {
                    Debug.LogError("This missile doesn't have a target.");
                }
                /*if (thisMissile.guidanceMode == Missile.GuidanceModes.Heat)//this code looks like absolute garbage, what was it even meant to do
                {//uwu
                    traverse2.Method("TrackHeat").GetValue();
                    ulong uid;
                    if (AIDictionaries.reverseAllActors.TryGetValue(thisMissile.heatSeeker.likelyTargetActor, out uid))
                    {
                        Debug.Log("IR CLIENT MISSILE: Fired on " + uid);
                    }
                    else
                    {
                        Debug.LogWarning("IR client missile does not have a target.");
                        missileSender.targetUID = uid;
                    }
                }*/
                missileSender.networkUID = networkUID;
                missileSender.ownerUID = lastChangeMessage.newOwnerUID;
                missileSender.hasFired = true;
                rbSender = gameObject.AddComponent<RigidbodyNetworker_Sender>();
                missileSender.rbSender = rbSender;
                rbSender.networkUID = networkUID;
                rbSender.ownerUID = lastChangeMessage.newOwnerUID;
                Debug.Log("Switched missile to our authority!");
            }
            else {
                Debug.Log("We should not be incharge of this missile");
                Destroy(missileSender);
                Destroy(rbSender);

                Rigidbody rb = GetComponent<Rigidbody>();
                rb.isKinematic = true;
                thisMissile.proxyDetonateRange = 0;

                missileReceiver = gameObject.AddComponent<MissileNetworker_Receiver>();
                missileReceiver.networkUID = networkUID;
                missileReceiver.ownerUID = lastChangeMessage.newOwnerUID;
                missileReceiver.hasFired = true;
                rbReceiver = gameObject.AddComponent<RigidbodyNetworker_Receiver>();
                missileReceiver.rbReceiver = rbReceiver;
                rbReceiver.networkUID = networkUID;
                rbReceiver.ownerUID = lastChangeMessage.newOwnerUID;
                Debug.Log("Switched missile to others authority!");
            }
            Debug.Log("Missile is owned by " + missileReceiver.ownerUID + " and has UID " + missileReceiver.rbReceiver.networkUID);
            currentLocalAuthority = targetLocalAuthority;
        }
        else
        {
            Debug.Log("Missile already has correct authority.");
        }
    }

    public void RequestUID(Packet packet)
    {
        Message_RequestNetworkUID lastMessage = ((PacketSingle)packet).message as Message_RequestNetworkUID;
        if (lastMessage.clientsUID != networkUID)
            return;
        networkUID = lastMessage.resultUID;
        Debug.Log($"Missile authority ({gameObject.name}) has received their UID from the host. \n Missiles UID = {networkUID}");
        Networker.RequestNetworkUID -= RequestUID;
    }

    public void OnDestroy()
    {
        Networker.MissileChangeAuthority -= MissileChangeAuthority;
    }
}
