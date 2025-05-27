using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class WheelProperties
{
    [HideInInspector] public TrailRenderer skidTrail;
    [HideInInspector] public GameObject skidTrailGameObject;

    public Vector3 localPosition;
    public float turnAngle = 30f;
    public float suspensionLength = 0.5f;

    [HideInInspector] public float lastSuspensionLength = 0.0f;
    public float mass = 16f;
    public float size = 0.5f;
    public float engineTorque = 40f;
    public float brakeStrength = 0.5f;
    public bool slidding = false;
    [HideInInspector] public Vector3 worldSlipDirection;
    [HideInInspector] public Vector3 suspensionForceDirection;
    [HideInInspector] public Vector3 wheelWorldPosition;
    [HideInInspector] public float wheelCircumference;
    [HideInInspector] public float torque = 0.0f;
    [HideInInspector] public GameObject wheelObject;
    [HideInInspector] public Vector3 localVelocity;
    [HideInInspector] public float normalForce;
    [HideInInspector] public float angularVelocity;
    [HideInInspector] public float slip;
    [HideInInspector] public Vector2 input = Vector2.zero;
    [HideInInspector] public float braking = 0;
}

public class Car : MonoBehaviour
{
    public GameObject skidMarkPrefab;
    public float smoothTurn = 0.03f;
    float coefStaticFriction = 2.95f;
    float coefKineticFriction = 0.85f;
    public GameObject wheelPrefab;
    public WheelProperties[] wheels;
    public float wheelGripX = 8f;
    public float wheelGripZ = 42f;
    public float suspensionForce = 90f;
    public float dampAmount = 2.5f;
    public float suspensionForceClamp = 200f;
    private Rigidbody rb;
    [HideInInspector] public bool forwards = true;


    // Assists
    public bool steeringAssist = true;
    public bool throttleAssist = true;
    public bool brakeAssist = true;
    [HideInInspector] public Vector2 userInput = Vector2.zero;
    public float downforce = 0.16f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        foreach (var w in wheels)
        {
            w.wheelObject = Instantiate(wheelPrefab, transform);
            w.wheelObject.transform.localPosition = w.localPosition;
            w.wheelObject.transform.eulerAngles = transform.eulerAngles;
            w.wheelObject.transform.localScale = 2f * new Vector3(w.size, w.size, w.size);
            w.wheelCircumference = 2f * Mathf.PI * w.size;

            if (skidMarkPrefab != null)
            {
                w.skidTrailGameObject = Instantiate(skidMarkPrefab, w.wheelObject.transform);
                w.skidTrailGameObject.transform.localPosition = Vector3.zero;
                w.skidTrailGameObject.transform.localRotation = Quaternion.identity;
                w.skidTrailGameObject.transform.parent = null;
                
                w.skidTrail = w.skidTrailGameObject.GetComponent<TrailRenderer>();
                if (w.skidTrail != null)
                    w.skidTrail.emitting = false;
            }
        }

        rb.centerOfMass += new Vector3(0, -0.5f, 0);
        rb.inertiaTensor *= 1.4f;
    }

    void Update()
    {
        // Get player input for reference
        userInput.x = Mathf.Lerp(userInput.x, Input.GetAxisRaw("Horizontal") / (1 + rb.linearVelocity.magnitude / 28f), 0.2f);
        userInput.y = Mathf.Lerp(userInput.y, Input.GetAxisRaw("Vertical"), 0.2f);
        bool isBraking = Input.GetKey(KeyCode.S) && forwards;
        if (isBraking) userInput.y = 0;

        float maxSlip = 0;
        // Calculate the maximum slip of all wheels
        for (int i = 0; i < wheels.Length; i++)
        {
            maxSlip = Mathf.Max(maxSlip, wheels[i].slip);
        }

        for (int i = 0; i < wheels.Length; i++)
        {
            if (throttleAssist && maxSlip > 0.96f)
            {
                // Reduce throttle input if slip is too high
                userInput.y = Mathf.Lerp(userInput.y, 0, maxSlip);
            }
            
            if (steeringAssist && maxSlip > 0.7f)
            {
                // Reduce steering input if slip is too high
                userInput.x = Mathf.Lerp(userInput.x, 0, 0.05f);
            }
            // Apply counter-steering when slipping severely
            if (maxSlip > 1.0f && wheels[i].localVelocity.magnitude > 0.1f)
            {
                // Calculate the angle between the wheel's forward direction and the sliding direction
                float angle = Mathf.Atan2(wheels[i].localVelocity.x, wheels[i].localVelocity.z) * Mathf.Rad2Deg;
                
                // Apply counter-steering to match the sliding direction
                wheels[i].input = new Vector2(
                    Mathf.Lerp(wheels[i].input.x, Mathf.Clamp(angle / wheels[i].turnAngle, -1f, 1f), 0.1f),
                    wheels[i].input.y
                );
            }

            if (brakeAssist && maxSlip > 0.99f)
            {
                // Reduce braking input if slip is too high
                isBraking = false;
            }

            wheels[i].braking = Mathf.Lerp(wheels[i].braking, (float)(isBraking ? 1 : 0), 0.2f);
            wheels[i].input = new Vector2(userInput.x, userInput.y);
        }
    }

    void FixedUpdate()
    {
        // Debug.Log(rb.velocity.magnitude);
        rb.AddForce(-transform.up * rb.linearVelocity.magnitude * downforce);
        foreach (var w in wheels)
        {
            RaycastHit hit;
            float rayLen = w.size * 2f + w.suspensionLength;
            Transform wheelObj = w.wheelObject.transform;
            Transform wheelVisual = wheelObj.GetChild(0);

            wheelObj.localRotation = Quaternion.Euler(0, w.turnAngle * w.input.x, 0);
            w.wheelWorldPosition = transform.TransformPoint(w.localPosition);
            Vector3 velocityAtWheel = rb.GetPointVelocity(w.wheelWorldPosition);
            w.localVelocity = wheelObj.InverseTransformDirection(velocityAtWheel);
            forwards = w.localVelocity.z > 0.1f;
            w.torque = w.engineTorque * w.input.y;

            float inertia = w.mass * w.size * w.size / 2f;
            float lateralVel = w.localVelocity.x;

            bool grounded = Physics.Raycast(w.wheelWorldPosition, -transform.up, out hit, rayLen);
            Vector3 worldVelAtHit = rb.GetPointVelocity(hit.point);
            float lateralHitVel = wheelObj.InverseTransformDirection(worldVelAtHit).x;

            float lateralFriction = -wheelGripX * lateralVel - 2f * lateralHitVel;
            float longitudinalFriction = -wheelGripZ * (w.localVelocity.z - w.angularVelocity * w.size);

            w.angularVelocity += (w.torque - longitudinalFriction * w.size) / inertia * Time.fixedDeltaTime;
            w.angularVelocity *= 1 - w.braking * w.brakeStrength * Time.fixedDeltaTime;
            if (Input.GetKey(KeyCode.Space)) // Handbrake
            {
                w.angularVelocity = 0;
            }

            Vector3 totalLocalForce = new Vector3(lateralFriction, 0f, longitudinalFriction)
                * w.normalForce * coefStaticFriction * Time.fixedDeltaTime;
            float currentMaxFrictionForce = w.normalForce * coefStaticFriction;

            w.slidding = totalLocalForce.magnitude > currentMaxFrictionForce;
            w.slip = totalLocalForce.magnitude / currentMaxFrictionForce;
            totalLocalForce = Vector3.ClampMagnitude(totalLocalForce, currentMaxFrictionForce);
            totalLocalForce *= w.slidding ? (coefKineticFriction / coefStaticFriction) : 1;

            Vector3 totalWorldForce = wheelObj.TransformDirection(totalLocalForce);
            w.worldSlipDirection = totalWorldForce;

            if (grounded)
            {
                float compression = rayLen - hit.distance;
                float damping = (w.lastSuspensionLength - hit.distance) * dampAmount;
                w.normalForce = (compression + damping) * suspensionForce;
                w.normalForce = Mathf.Clamp(w.normalForce, 0f, suspensionForceClamp);

                Vector3 springDir = hit.normal * w.normalForce;
                w.suspensionForceDirection = springDir;

                rb.AddForceAtPosition(springDir + totalWorldForce, hit.point);
                w.lastSuspensionLength = hit.distance;
                wheelObj.position = hit.point + transform.up * w.size;

                if (w.slidding)
                {
                    // If no skid trail exists or if it was detached previously, instantiate a new one.
                    if (w.skidTrail == null && skidMarkPrefab != null)
                    {
                        GameObject skidTrailObj = Instantiate(skidMarkPrefab, transform);
                        skidTrailObj.transform.SetParent(w.wheelObject.transform);
                        skidTrailObj.transform.localPosition = Vector3.zero;
                        w.skidTrail = skidTrailObj.GetComponent<TrailRenderer>();
                        w.skidTrail.time = 3f; // Trail lasts for 10 seconds
                        w.skidTrail.autodestruct = true;
                        if (w.skidTrail != null)
                        {
                            w.skidTrail.emitting = true;
                        }
                    }
                    else if (w.skidTrail != null)
                    {
                        // Continue emitting and update its position to the contact point.
                        w.skidTrail.emitting = true;
                        w.skidTrail.transform.position = hit.point;
                        // Align the skid trail so its up vector is the road normal.
                        // This projects the wheel's forward direction onto the road plane to preserve skid direction.
                        // Now update to real position/rotation
                        w.skidTrail.transform.position = hit.point;

                        Vector3 skidDir = Vector3.ProjectOnPlane(w.worldSlipDirection.normalized, hit.normal);
                        if (skidDir.sqrMagnitude < 0.001f)
                            skidDir = Vector3.ProjectOnPlane(wheelObj.forward, hit.normal).normalized;

                        Quaternion flatRot = Quaternion.LookRotation(skidDir, hit.normal)
                                            * Quaternion.Euler(90f, 0f, 0f);
                        w.skidTrail.transform.rotation = flatRot;
                    }
                }
                else if (w.skidTrail != null && w.skidTrail.emitting)
                {
                    // Stop emitting and detach the skid trail so it remains in the scene to fade out.
                    w.skidTrail.emitting = false;
                    w.skidTrail.transform.parent = null;
                    // Optionally, destroy the skid trail after its lifetime has elapsed.
                    Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    w.skidTrail = null;
                }
            }
            else
            {
                wheelObj.position = w.wheelWorldPosition + transform.up * (w.size - rayLen);
                if (w.skidTrail != null && w.skidTrail.emitting)
                {
                    w.skidTrail.emitting = false;
                    w.skidTrail.transform.parent = null;
                    Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                    w.skidTrail = null;
                }
            }
            wheelVisual.Rotate(
                Vector3.right,
                w.angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime,
                Space.Self
            );
        }
    }
}