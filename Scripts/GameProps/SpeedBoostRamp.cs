using UnityEngine;
using System.Collections;

/// <summary>
/// Speed boost power-up ramp that increases vehicle speed and reduces downforce temporarily.
/// Place this script on a GameObject with a Trigger Collider at the ramp location.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SpeedBoostRamp : MonoBehaviour {

    [Header("Boost Settings")]
    [Tooltip("Multiplier for engine torque during boost")]
    [Range(1.5f, 5f)]
    public float speedMultiplier = 2.5f;

    [Tooltip("Multiplier for maximum speed during boost")]
    [Range(1.5f, 3f)]
    public float maxSpeedMultiplier = 1.8f;

    [Tooltip("How much to reduce downforce (lower = less downforce)")]
    [Range(0.1f, 0.8f)]
    public float downforceReduction = 0.5f;

    [Tooltip("Duration of the boost effect in seconds")]
    public float boostDuration = 3f;

    [Header("Jump Assistance")]
    [Tooltip("Additional upward force when hitting the ramp")]
    public float jumpAssistForce = 5f;

    [Tooltip("Forward force to maintain momentum during jump")]
    public float forwardAssistForce = 10f;

    [Header("Visual Effects")]
    [Tooltip("Particle effect to spawn when boost is activated")]
    public GameObject boostActivationEffect;

    [Tooltip("Particle effect that follows the car during boost")]
    public GameObject boostTrailEffect;

    [Tooltip("Optional: Audio clip to play on boost activation")]
    public AudioClip boostSound;

    [Header("Cooldown")]
    [Tooltip("Prevent retriggering boost if vehicle re-enters trigger")]
    public bool useCooldown = true;
    public float cooldownTime = 5f;

    private AudioSource audioSource;
    private float lastBoostTime = -999f;
    private GameObject currentTrailEffect;

    private void Start() {
        // Ensure the collider is set as trigger
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        // Setup audio source if boost sound is assigned
        if (boostSound != null) {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = boostSound;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
        }
    }

    private void OnTriggerEnter(Collider other) {
        // Check if the object entering has the SCC_Drivetrain component
        SCC_Drivetrain drivetrain = other.GetComponentInParent<SCC_Drivetrain>();

        if (drivetrain != null) {
            // Check cooldown
            if (useCooldown && Time.time - lastBoostTime < cooldownTime) {
                return;
            }

            // Activate the boost
            StartCoroutine(ApplySpeedBoost(drivetrain));
            lastBoostTime = Time.time;

            // Play visual effects
            if (boostActivationEffect != null) {
                Instantiate(boostActivationEffect, transform.position, Quaternion.identity);
            }

            // Play sound effect
            if (audioSource != null && boostSound != null) {
                audioSource.Play();
            }

            Debug.Log($"Speed Boost Activated! Car speed: {drivetrain.speed:F1} km/h");
        }
    }

    private IEnumerator ApplySpeedBoost(SCC_Drivetrain drivetrain) {
        Rigidbody carRigidbody = drivetrain.GetComponent<Rigidbody>();
        Transform carTransform = drivetrain.transform;

        // Store original values
        float originalEngineTorque = drivetrain.engineTorque;
        float originalMaxSpeed = drivetrain.maximumSpeed;
        float originalDrag = carRigidbody.linearDamping;
        float originalAngularDrag = carRigidbody.angularDamping;

        // Apply boost effects
        drivetrain.engineTorque *= speedMultiplier;
        drivetrain.maximumSpeed *= maxSpeedMultiplier;
        
        // Reduce downforce by reducing drag
        carRigidbody.linearDamping *= downforceReduction;
        carRigidbody.angularDamping *= downforceReduction;

        // Apply jump assist forces
        Vector3 jumpForce = (Vector3.up * jumpAssistForce * carRigidbody.mass) + 
                           (carTransform.forward * forwardAssistForce * carRigidbody.mass);
        carRigidbody.AddForce(jumpForce, ForceMode.Impulse);

        // Start boost trail effect
        if (boostTrailEffect != null) {
            currentTrailEffect = Instantiate(boostTrailEffect, carTransform.position, Quaternion.identity);
            currentTrailEffect.transform.SetParent(carTransform);
        }

        // Optional: Add visual feedback to the car
        StartCoroutine(FlashCarLights(drivetrain));

        Debug.Log($"Boost Applied! Torque: {drivetrain.engineTorque:F0}, Max Speed: {drivetrain.maximumSpeed:F0}");

        // Wait for boost duration
        float timer = 0f;
        while (timer < boostDuration) {
            timer += Time.deltaTime;
            
            // Gradually reduce the boost effect for smoother transition
            if (timer > boostDuration * 0.7f) {
                float fade = (boostDuration - timer) / (boostDuration * 0.3f);
                drivetrain.engineTorque = Mathf.Lerp(originalEngineTorque, originalEngineTorque * speedMultiplier, fade);
                drivetrain.maximumSpeed = Mathf.Lerp(originalMaxSpeed, originalMaxSpeed * maxSpeedMultiplier, fade);
            }
            
            yield return null;
        }

        // Restore original values
        drivetrain.engineTorque = originalEngineTorque;
        drivetrain.maximumSpeed = originalMaxSpeed;
        carRigidbody.linearDamping = originalDrag;
        carRigidbody.angularDamping = originalAngularDrag;

        // Remove trail effect
        if (currentTrailEffect != null) {
            Destroy(currentTrailEffect);
        }

        Debug.Log("Boost Ended - Normal driving restored");
    }

    private IEnumerator FlashCarLights(SCC_Drivetrain drivetrain) {
        // This would require you to have light components on your car
        // You can modify this based on your car's setup
        Light[] carLights = drivetrain.GetComponentsInChildren<Light>();
        Color originalColor = Color.white;
        
        if (carLights.Length > 0) {
            originalColor = carLights[0].color;
            
            // Flash lights during boost
            for (int i = 0; i < 6; i++) {
                foreach (Light light in carLights) {
                    light.color = i % 2 == 0 ? Color.cyan : originalColor;
                }
                yield return new WaitForSeconds(0.2f);
            }
            
            // Restore original light colors
            foreach (Light light in carLights) {
                light.color = originalColor;
            }
        }
    }

    // Visual debugging in editor
    private void OnDrawGizmos() {
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Collider col = GetComponent<Collider>();
        if (col != null) {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box) {
                Gizmos.DrawCube(box.center, box.size);
            } else if (col is SphereCollider sphere) {
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
        }

        // Draw boost direction arrow
        Gizmos.color = Color.cyan;
        Vector3 start = transform.position;
        Vector3 end = start + transform.forward * 3f;
        Gizmos.DrawRay(start, transform.forward * 3f);
        
        // Draw arrow head
        Vector3 right = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(0, 160, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(0, 200, 0) * Vector3.forward;
        Gizmos.DrawRay(end, right * 0.5f);
        Gizmos.DrawRay(end, left * 0.5f);
    }

    private void OnDrawGizmosSelected() {
        // Draw activation zone when selected
        Gizmos.color = Color.green;
        Collider col = GetComponent<Collider>();
        if (col != null) {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box) {
                Gizmos.DrawWireCube(box.center, box.size);
            } else if (col is SphereCollider sphere) {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
        
        // Draw jump assist visualization
        Gizmos.color = Color.yellow;
        Vector3 jumpVector = (Vector3.up * jumpAssistForce * 0.5f) + (transform.forward * forwardAssistForce * 0.5f);
        Gizmos.DrawRay(transform.position, jumpVector);
    }
}