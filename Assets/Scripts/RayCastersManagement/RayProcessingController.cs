﻿using System;
using UnityEngine;

/// <summary>
/// This class controls the processing of ray hits.
/// The game object with this script is the ray receiver itself.
/// </summary>
public class RayProcessingController : MonoBehaviour
{
    public enum ReceiverState
    {
        Idle,
        ActionExecuted,
        PartialHit
    }

    [Tooltip("Enable/disable the reflection of the ray.")]
    public bool enableReflection;

    [Tooltip("List of actions to perform on hit.")]
    public ActionableObject[] actionableObjects;

    [Tooltip("Line renderer prefab for reflections.")]
    public LineRenderer reflectionLineRendererPrefab;

    [Tooltip("Limit for recursive reflections.")]
    public int reflectionLimit = 5;

    [HideInInspector] public ReceiverState receiverState = ReceiverState.Idle;
    [HideInInspector] public int rayHits = 0;
    [HideInInspector] public bool isActivated = false;

    private LineRenderer reflectionLineRenderer;
    public LineRenderer currentReflectionLine;

    [HideInInspector] public bool levelCompleted = false;

    [HideInInspector] public bool actionsExecuted = false;

    public Material reflectionMaterial;


    [System.Serializable]
    public class ActionableObject
    {
        public GameObject targetObject;
        public ActionBase actionBase;
        public float transparencyIncrement = 0.1f; // Default value
    }

    private RayActivationController rayActivationController;

    private void Start()
    {
        rayActivationController = FindObjectOfType<RayActivationController>();
    }

    private void FixedUpdate()
    {
        if (receiverState == ReceiverState.ActionExecuted && !actionsExecuted)
        {
            CheckRayExit();
        }
    }

    private void CheckRayExit()
    {
        Ray ray = new Ray(rayActivationController.raycastOrigin.position,
            rayActivationController.raycastOrigin.forward);
        RaycastHit hit;
        bool hitSomething = Physics.Raycast(ray, out hit, Mathf.Infinity, rayActivationController.raycastMask);

        if (!hitSomething || hit.collider.gameObject != gameObject)
        {
            ResetRayHit();
        }
    }

    public void ProcessRayHit(Vector3 hitPoint, Ray incomingRay, Vector3 normal)
    {
        if (receiverState == ReceiverState.ActionExecuted || actionsExecuted)
        {
            if (enableReflection)
            {
                // Create the reflection line
                if (currentReflectionLine == null)
                {
                    currentReflectionLine = CreateReflectionLineRenderer(hitPoint, hitPoint, reflectionMaterial);
                }

                ReflectRay(hitPoint, incomingRay.direction, normal, 0);
            }

            return;
        }

        if (enableReflection)
        {
            // Create the reflection line
            if (currentReflectionLine == null)
            {
                currentReflectionLine = CreateReflectionLineRenderer(hitPoint, hitPoint);
            }

            ReflectRay(hitPoint, incomingRay.direction, normal, 0);
        }

        rayHits++;
        Debug.Log($"Ray hit receiver {gameObject.name}. Total hits: {rayHits}");

        if (rayHits >= 1)
        {
            ExecuteActionsAndResetRayHits(incomingRay);
            receiverState = ReceiverState.ActionExecuted;
        }
        else
        {
            RevertActions();
            receiverState = ReceiverState.PartialHit;
        }

        // Reset ray hit if the ray is no longer hitting the mirror's surface
        if (receiverState != ReceiverState.ActionExecuted && !Physics.Raycast(incomingRay))
        {
            ResetRayHit();
        }
    }


    private void ExecuteActionsAndResetRayHits(Ray incomingRay)
    {
        Debug.Log($"Receiver {gameObject.name} achieved required hits.");
        receiverState = ReceiverState.ActionExecuted;
        actionsExecuted = true;

        isActivated = true;
        levelCompleted = true;


        try
        {
            foreach (ActionableObject action in actionableObjects)
            {
                action.actionBase.ExecuteAction(action.targetObject, incomingRay, action.transparencyIncrement);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error executing actions for receiver {gameObject.name}: {e.Message}");
            receiverState = ReceiverState.PartialHit;
            RevertActions();
            return;
        }

        rayHits = 0;
    }

    private RayProcessingController lastHitReceiver;

    private void ReflectRay(Vector3 origin, Vector3 direction, Vector3 normal, int reflectionCount)
    {
        Debug.Log("ReflectRay called: reflectionCount = " + reflectionCount);
        if (reflectionCount >= reflectionLimit) return;

        Vector3 reflectedDirection = Vector3.Reflect(direction, normal);
        Ray ray = new Ray(origin, reflectedDirection);
        RaycastHit hit;

        Vector3 endPoint = origin + reflectedDirection * 1000; // default end point if no hit

        bool hitValidReceiver = false;

        if (Physics.Raycast(ray, out hit))
        {
            Debug.Log("Ray hit: " + hit.collider.gameObject.name);
            endPoint = hit.point;

            RayProcessingController hitReceiver = hit.collider.gameObject.GetComponent<RayProcessingController>();
            if (hitReceiver != null)
            {
                Debug.Log("Valid receiver found: " + hitReceiver.gameObject.name);

                if (hitReceiver.receiverState != ReceiverState.ActionExecuted)
                {
                    hitReceiver.ProcessRayHit(hit.point, ray, hit.normal);
                    hitValidReceiver = true;
                    lastHitReceiver = hitReceiver;
                }
            }
        }
        else
        {
            Debug.Log("Ray did not hit any object");
        }

        // Update the reflection line
        if (currentReflectionLine != null)
        {
            currentReflectionLine.SetPosition(0, origin); // update start position
            currentReflectionLine.SetPosition(1, endPoint);
        }

        // Reset ray hit if the reflected ray is no longer hitting a valid receiver
        if (!hitValidReceiver && lastHitReceiver != null)
        {
            lastHitReceiver.ResetRayHit();
            lastHitReceiver = null;
        }
    }


    public LineRenderer CreateReflectionLineRenderer(Vector3 start, Vector3 end, Material material = null)
    {
        Debug.Log("CreateReflectionLineRenderer called with start: " + start + ", end: " + end);

        LineRenderer lineRenderer;
        if (reflectionLineRendererPrefab != null)
        {
            lineRenderer = Instantiate(reflectionLineRendererPrefab, transform);
        }
        else
        {
            GameObject reflectionLineObject = new GameObject("ReflectionLine");
            reflectionLineObject.transform.SetParent(transform);
            lineRenderer = reflectionLineObject.AddComponent<LineRenderer>();
            lineRenderer.material = material != null ? material : new Material(Shader.Find("Standard"));
            lineRenderer.startColor = Color.yellow;
            lineRenderer.endColor = Color.yellow;
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.positionCount = 2;
        }

        lineRenderer.useWorldSpace = true;

        // Set positions for the line renderer
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        return lineRenderer;
    }

    public void DestroyReflectionLine()
    {
        if (currentReflectionLine != null)
        {
            Destroy(currentReflectionLine.gameObject);
            currentReflectionLine = null;
        }
    }

    public void ResetRayHit()
    {
        Debug.Log($"Resetting ray hits for receiver {gameObject.name}");

        if (levelCompleted)
        {
            foreach (ActionableObject action in actionableObjects)
            {
                action.actionBase.RevertAction(action.targetObject);
            }

            levelCompleted = false;
        }

        actionsExecuted = false;
        receiverState = ReceiverState.Idle;
        rayHits = 0;
        isActivated = false;

        // Destroy the reflection line
        DestroyReflectionLine();
    }


    private void RevertActions()
    {
        foreach (ActionableObject action in actionableObjects)
        {
            action.actionBase.RevertAction(action.targetObject);
        }
    }

    private void OnEnable()
    {
        ResetRayHit();
    }
}