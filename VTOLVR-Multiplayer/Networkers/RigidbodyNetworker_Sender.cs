﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
public class RigidbodyNetworker_Sender : MonoBehaviour
{
    public ulong networkUID;
    private Rigidbody rb;
    private Message_RigidbodyUpdate lastMessage;
    public Vector3 originOffset;
    private Vector3D globalLastPosition;
    private Vector3 localLastPosition;
    private Vector3 lastVelocity;
    private Vector3 lastUp;
    private Vector3 lastForward;
    private Quaternion lastRotation;
    private Vector3 lastAngularVelocity;
    private float threshold = 0.5f;
    private float angleThreshold = 1f;
    
    private ulong updateNumber;
    private float tick;
    private float tickRate = 10;

    public int first = 0;
    public bool player = false;

    public Vector3D spawnPosf;
    public Quaternion spawnRotf;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastMessage = new Message_RigidbodyUpdate(new Vector3D(), new Vector3D(), new Vector3D(), Quaternion.identity, 0, networkUID);
        tick = 0;
    }

    private void FixedUpdate()
    {
         if(first < 200)if(player)
        {
            PlayerVehicle currentVehiclet = PilotSaveManager.currentVehicle;
                    GameObject localVehicle = VTOLAPI.GetPlayersVehicleGameObject();
                localVehicle.transform.position = VTMapManager.GlobalToWorldPoint(spawnPosf);
            localVehicle.transform.position += currentVehiclet.playerSpawnOffset + new Vector3(0f, 0.5f, 0f);
                localVehicle.transform.rotation = spawnRotf;

                //need to add airspeed for air spawn
               rb.velocity = new Vector3(0, 0, 0); rb.Sleep();
                Physics.SyncTransforms();
                first++;
                
        }
        globalLastPosition += new Vector3D(lastVelocity * Time.fixedDeltaTime);
        localLastPosition = VTMapManager.GlobalToWorldPoint(globalLastPosition);
        Quaternion quatVel = Quaternion.Euler(lastAngularVelocity * Time.fixedDeltaTime);
        lastRotation *= quatVel;

        lastUp = lastRotation * Vector3.up;
        lastForward = lastRotation * Vector3.forward;
        tick += Time.fixedDeltaTime;
        if (tick > 1/tickRate || Vector3.Distance(localLastPosition, transform.TransformPoint(originOffset)) > threshold || Vector3.Angle(lastUp, transform.up) > angleThreshold || Vector3.Angle(lastForward, transform.forward) > angleThreshold)
        {
            tick = 0;
            lastUp = transform.up;
            lastForward = transform.forward;

            globalLastPosition = VTMapManager.WorldToGlobalPoint(transform.TransformPoint(originOffset));
            lastVelocity = rb.velocity;

            lastRotation = transform.rotation;
            lastAngularVelocity = rb.angularVelocity * Mathf.Rad2Deg;

            lastMessage.position = VTMapManager.WorldToGlobalPoint(transform.TransformPoint(originOffset));
            lastMessage.rotation = transform.rotation;
            if (Multiplayer.SoloTesting)
                lastMessage.position += new Vector3D(-30, 0, 0);
            lastMessage.velocity = new Vector3D(rb.velocity);
            lastMessage.angularVelocity = new Vector3D(rb.angularVelocity * Mathf.Rad2Deg);
            lastMessage.networkUID = networkUID;
            lastMessage.sequenceNumber = ++updateNumber;
            if (Networker.isHost)
                NetworkSenderThread.Instance.SendPacketAsHostToAllClients(lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliableNoDelay);
            else
                NetworkSenderThread.Instance.SendPacketToSpecificPlayer(Networker.hostID, lastMessage, Steamworks.EP2PSend.k_EP2PSendUnreliableNoDelay);
        }
            // Temperz STOP KILLING PERFORMANCE AND HARD DRIVES!
            //Debug.Log($"{actor.name} is not outside of the threshold {Threshold}, the distance is {Vector3.Distance(lastPos, gameObject.transform.position)} not updating it.");
    }

    public void SetSpawn(Vector3D spawnPos, Quaternion spawnRot)
    {
        Debug.Log($"starting spawn repositioner");
        spawnPosf = spawnPos;
        spawnRotf = spawnRot;
        StartCoroutine(SetSpawnEnumerator(spawnPos, spawnRot));
    }

    private IEnumerator SetSpawnEnumerator(Vector3D spawnPos, Quaternion spawnRot)
    {
         
        rb.velocity = new Vector3(0, 0, 0); rb.Sleep();

        rb.transform.position = VTMapManager.GlobalToWorldPoint(spawnPos);
        rb.transform.rotation = spawnRot;
        rb.Sleep();

        Physics.SyncTransforms();
        Debug.Log($"Our position is now {rb.position}");
   
        yield return new WaitForSeconds(0.5f);
        rb.detectCollisions = true;

    }
}
