using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class trailerConnection : MonoBehaviour
{
    public float strengthMultiplier = 1.0f;

    public GameObject trailer;
    public GameObject connectionPointOnTrailer;
    public GameObject vehicle;
    public GameObject connectionPointOnVehicle;
    public float setDistance = 0.8f;
    float lastDistance = -1.0f; // -1 sentinel: will be initialised in Start
    float strength = 40.0f;
    // Effective damping coefficient is damp / fixedDeltaTime (see FixedUpdate).
    // 8 / 0.02 = 400, matching the previous behaviour but now timestep-independent.
    float damp = 8.0f;
    Rigidbody VehicleRb;
    Rigidbody TrailerRb;
    LineRenderer lineRenderer;

    void Start()
    {
        VehicleRb = vehicle.GetComponent<Rigidbody>();
        TrailerRb = trailer.GetComponent<Rigidbody>();
        lineRenderer = GetComponent<LineRenderer>();
        // Seed lastDistance with the real initial distance so the first
        // FixedUpdate doesn't apply a spurious damping impulse.
        lastDistance = Vector3.Distance(
            connectionPointOnTrailer.transform.position,
            connectionPointOnVehicle.transform.position);
    }

    void Update()
    {
        if (lineRenderer == null) return;
        lineRenderer.SetPosition(0, connectionPointOnTrailer.transform.position);
        lineRenderer.SetPosition(1, connectionPointOnVehicle.transform.position);
    }

    void FixedUpdate()
    {
        if (VehicleRb == null)  VehicleRb  = vehicle.GetComponent<Rigidbody>();
        if (TrailerRb == null)  TrailerRb  = trailer.GetComponent<Rigidbody>();

        float dist = Vector3.Distance(
            connectionPointOnTrailer.transform.position,
            connectionPointOnVehicle.transform.position);

        // Avoid division by zero when the connection points coincide.
        // The spring will push them apart next frame once there is a valid direction.
        if (dist < 0.001f)
        {
            lastDistance = dist;
            return;
        }

        // Safe normalised direction (no .normalized on a near-zero vector).
        Vector3 direction = (connectionPointOnTrailer.transform.position
                           - connectionPointOnVehicle.transform.position) / dist;

        float pushPull = ((setDistance - dist) * strength
                        + (lastDistance - dist) / Time.fixedDeltaTime * damp)
                        * strengthMultiplier;

        Vector3 force = pushPull * direction;
        VehicleRb.AddForceAtPosition(-force, connectionPointOnVehicle.transform.position);
        TrailerRb.AddForceAtPosition( force, connectionPointOnTrailer.transform.position);
        lastDistance = dist;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if (connectionPointOnTrailer != null && connectionPointOnVehicle != null) {
            Gizmos.DrawLine(connectionPointOnTrailer.transform.position, connectionPointOnVehicle.transform.position);
        }
    }
}