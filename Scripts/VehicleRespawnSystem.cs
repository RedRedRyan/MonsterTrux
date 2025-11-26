using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VehicleRespawnSystem : MonoBehaviour
{
    [Header("Respawn Settings")]
    public float respawnHeight = 20f;
    public float forwardOffset = 1f;
    public float uprightThreshold = 0.6f;
    public float historyInterval = 0.5f;
    public int maxHistoryPoints = 20;
    
    [Header("Detection Settings")]
    public string waterTag = "Water";
    public LayerMask groundLayerMask;
    
    [Header("UI Settings")]
    public Button respawnButton; // Changed to Button type for direct reference
    public float stuckCheckInterval = 1f;
    public float minMovementForStuck = 0.5f;
    
    // Vehicle components
    private Rigidbody vehicleRigidbody;
    private Transform vehicleTransform;
    
    // State tracking
    private bool isInWater = false;
    private bool isFlipped = false;
    private bool isStuck = false;
    private float flipTimer = 0f;
    private float waterTimer = 0f;
    private float stuckTimer = 0f;
    private Vector3 lastPosition;
    
    // Position history
    private Queue<HistoryPoint> positionHistory = new Queue<HistoryPoint>();
    private float lastHistoryRecordTime = 0f;
    private float lastStuckCheckTime = 0f;
    
    // History point structure
    private struct HistoryPoint
    {
        public Vector3 position;
        public Quaternion rotation;
        public bool wasUpright;
        
        public HistoryPoint(Vector3 pos, Quaternion rot, bool upright)
        {
            position = pos;
            rotation = rot;
            wasUpright = upright;
        }
    }

    void Start()
    {
        vehicleRigidbody = GetComponent<Rigidbody>();
        vehicleTransform = transform;
        lastPosition = vehicleTransform.position;
        
        // Set up button listener
        if (respawnButton != null)
        {
            respawnButton.onClick.AddListener(RespawnVehicle);
            respawnButton.gameObject.SetActive(false);
        }
        
        // Record initial position
        RecordHistoryPoint();
    }

    void Update()
    {
        // Check vehicle state
        CheckIfFlipped();
        CheckIfStuck();
        
        // Record position history at intervals
        if (Time.time - lastHistoryRecordTime >= historyInterval)
        {
            RecordHistoryPoint();
            lastHistoryRecordTime = Time.time;
        }
        
        // Check if we need to respawn automatically
        if (isInWater)
        {
            waterTimer += Time.deltaTime;
            if (waterTimer > 2f) // 2 seconds in water
            {
                RespawnVehicle();
            }
        }
        else
        {
            waterTimer = 0f;
        }
        
        if (isFlipped)
        {
            flipTimer += Time.deltaTime;
            if (flipTimer > 3f) // 3 seconds flipped
            {
                RespawnVehicle();
            }
        }
        else
        {
            flipTimer = 0f;
        }
        
        // Show/hide respawn button based on stuck status
        UpdateRespawnButton();
    }
    
    void CheckIfStuck()
    {
        // Check at intervals
        if (Time.time - lastStuckCheckTime < stuckCheckInterval) return;
        lastStuckCheckTime = Time.time;
        
        // Calculate distance moved since last check
        float distanceMoved = Vector3.Distance(vehicleTransform.position, lastPosition);
        lastPosition = vehicleTransform.position;
        
        // If not moving much, increment stuck timer
        if (distanceMoved < minMovementForStuck)
        {
            stuckTimer += stuckCheckInterval;
            
            // If stuck for more than 5 seconds, mark as stuck
            if (stuckTimer > 5f)
            {
                isStuck = true;
            }
        }
        else
        {
            // Reset stuck timer if moving
            stuckTimer = 0f;
            isStuck = false;
        }
    }
    
    void UpdateRespawnButton()
    {
        if (respawnButton == null) return;
        
        // Show button if vehicle is stuck, flipped, or in water
        bool shouldShow = isStuck || isFlipped || isInWater;
        respawnButton.gameObject.SetActive(shouldShow);
    }
    
    void RecordHistoryPoint()
    {
        // Check if vehicle is upright
        bool upright = Vector3.Dot(vehicleTransform.up, Vector3.up) > uprightThreshold;
        
        // Create new history point
        HistoryPoint point = new HistoryPoint(
            vehicleTransform.position,
            vehicleTransform.rotation,
            upright
        );
        
        // Add to history
        positionHistory.Enqueue(point);
        
        // Remove oldest point if we exceed max history
        if (positionHistory.Count > maxHistoryPoints)
        {
            positionHistory.Dequeue();
        }
    }
    
    void CheckIfFlipped()
    {
        isFlipped = Vector3.Dot(vehicleTransform.up, Vector3.up) < uprightThreshold;
    }
    
    // Public method to be called by the UI button
    public void RespawnVehicle()
    {
        // Find the best respawn point from history
        HistoryPoint? bestPoint = FindBestRespawnPoint();
        
        if (bestPoint.HasValue)
        {
            // Respawn 20m above and 1m ahead of the best historical point
            Vector3 respawnPosition = bestPoint.Value.position + 
                                    Vector3.up * respawnHeight + 
                                    bestPoint.Value.rotation * Vector3.forward * forwardOffset;
            
            vehicleTransform.position = respawnPosition;
            vehicleTransform.rotation = bestPoint.Value.rotation;
        }
        else
        {
            // Fallback: respawn 20m above and 1m ahead of current position
            Vector3 respawnPosition = vehicleTransform.position + 
                                    Vector3.up * respawnHeight + 
                                    vehicleTransform.forward * forwardOffset;
            
            vehicleTransform.position = respawnPosition;
            // Keep current rotation
        }
        
        // Reset physics
        vehicleRigidbody.linearVelocity = Vector3.zero;
        vehicleRigidbody.angularVelocity = Vector3.zero;
        
        // Reset all timers and states
        waterTimer = 0f;
        flipTimer = 0f;
        stuckTimer = 0f;
        isInWater = false;
        isStuck = false;
        
        // Update last position to prevent immediate stuck detection
        lastPosition = vehicleTransform.position;
        
        // Hide respawn button after respawning
        if (respawnButton != null)
        {
            respawnButton.gameObject.SetActive(false);
        }
    }
    
    HistoryPoint? FindBestRespawnPoint()
    {
        HistoryPoint? bestPoint = null;
        
        // Look through history to find the most recent point that was upright
        foreach (HistoryPoint point in positionHistory)
        {
            if (point.wasUpright)
            {
                bestPoint = point;
                // Continue to find the most recent one
            }
        }
        
        return bestPoint;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(waterTag))
        {
            isInWater = true;
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(waterTag))
        {
            isInWater = false;
            waterTimer = 0f;
        }
    }
    
    // Clean up button listener when destroyed
    void OnDestroy()
    {
        if (respawnButton != null)
        {
            respawnButton.onClick.RemoveListener(RespawnVehicle);
        }
    }
    
    // Visual debugging in editor
    void OnDrawGizmosSelected()
    {
        // Draw history points
        Gizmos.color = Color.blue;
        foreach (HistoryPoint point in positionHistory)
        {
            Gizmos.DrawWireSphere(point.position, 0.5f);
        }
    }
}