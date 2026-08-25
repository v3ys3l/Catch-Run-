using Mirror;
using UnityEngine;
using TMPro;
using UnityEngine.UI; // For the "Press E to pick up" UI text

public class PlayerInteraction : NetworkBehaviour
{
    public float pickupDistance = 1.5f;
    public LayerMask throwableLayerMask; // Set this in Inspector to only detect ThrowableObjects
    public Transform holdPoint; // Assign a child GameObject on player where the object will be held
    public Image pickupPromptImage; 

    [SyncVar(hook = nameof(OnHeldObjectChanged))]
    private uint heldObjectNetId = 0; 
    private ThrowableObject currentHeldObject; // Local reference to the held object

    private ThrowableObject objectInRange;
    private float timeHeld = 0f;
    public float maxHoldTime = 5f; // Max time to hold an object before dropping

    public AudioSource audioSourcecatch;
    public AudioSource audioSourcethrow;

    void Start()
    {
        if (pickupPromptImage != null) pickupPromptImage.gameObject.SetActive(false);
        if (holdPoint == null)
        {
            // Create a default hold point if not assigned
            GameObject hp = new GameObject("HoldPoint");
            hp.transform.SetParent(transform);
            hp.transform.localPosition = new Vector3(0.5f, 0, 0); // Default offset
            holdPoint = hp.transform;
            Debug.LogWarning($"[PlayerInteraction] HoldPoint not assigned for {gameObject.name}, created a default one. Adjust its position if needed.");
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // --- Object Detection and Pickup Prompt ---
        if (heldObjectNetId == 0) // If not holding anything
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, pickupDistance, throwableLayerMask);
            objectInRange = null; // Reset
            float closestDistSqr = pickupDistance * pickupDistance;

            foreach (Collider2D col in colliders)
            {
                ThrowableObject throwable = col.GetComponent<ThrowableObject>();
                if (throwable != null && throwable.holderNetId == 0) // If it's a throwable and not held by anyone
                {
                    float distSqr = (col.transform.position - transform.position).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        objectInRange = throwable;
                        closestDistSqr = distSqr;
                    }
                }
            }

            if (objectInRange != null)
            {
                if (pickupPromptImage != null)
                {
                    // No text to set for an image, just activate it
                    pickupPromptImage.gameObject.SetActive(true);
                }
            }
            else
            {
                if (pickupPromptImage != null) pickupPromptImage.gameObject.SetActive(false);
            }
        }
        else // Holding an object
        {
            if (pickupPromptImage != null) pickupPromptImage.gameObject.SetActive(false); // Hide prompt if holding
            
            timeHeld += Time.deltaTime;
            if (timeHeld >= maxHoldTime)
            {
                Debug.Log($"[PlayerInteraction] Held object for too long. Dropping.");
                CmdDropObject();
                timeHeld = 0f; // Reset timer
            }
        }

        // --- Input Handling ---
        if (Input.GetKeyDown(KeyCode.E)) // Reverted to KeyCode.E
        {
            if (heldObjectNetId == 0) // Not holding anything, try to pick up
            {
                if (objectInRange != null)
                {
                    audioSourcecatch.Play();
                    CmdPickupObject(objectInRange.netId);
                }
            }
            else // Holding something, drop it (E key also drops)
            {
                CmdDropObject();
            }
        }

        // --- Throwing Logic ---
        if (heldObjectNetId != 0 && Input.GetMouseButtonDown(0)) // Reverted to MouseButtonDown(0)
        {
            Debug.Log($"[PlayerInteraction Update] Throw input detected (Mouse). Held Object NetID: {heldObjectNetId}");
            audioSourcethrow.Play();
            Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPosition.z = transform.position.z; // Keep it in 2D plane
            Vector2 throwDirection = (mouseWorldPosition - transform.position).normalized;
            
            CmdThrowObject(throwDirection, 15f); // Example throw force of 15
            timeHeld = 0f; // Reset hold timer as object is thrown
        }
    }

    [Command]
    void CmdPickupObject(uint objectNetId)
    {
        if (NetworkServer.spawned.TryGetValue(objectNetId, out NetworkIdentity objectIdentity))
        {
            ThrowableObject throwable = objectIdentity.GetComponent<ThrowableObject>();
            if (throwable != null && throwable.holderNetId == 0) // Check again on server if it's still available
            {
                throwable.Pickup(this.netIdentity); // Pass this player's NetworkIdentity
                heldObjectNetId = objectNetId; // Server sets this SyncVar
                // currentHeldObject = throwable; // Server-side reference
                timeHeld = 0f; // Reset hold timer on server (though client also tracks for UI/prediction)
                Debug.Log($"[PlayerInteraction CmdPickup] Player {netId} picked up object {objectNetId}");
            }
        }
    }

    [Command]
    void CmdDropObject()
    {
        if (heldObjectNetId != 0 && NetworkServer.spawned.TryGetValue(heldObjectNetId, out NetworkIdentity objectIdentity))
        {
            ThrowableObject throwable = objectIdentity.GetComponent<ThrowableObject>();
            if (throwable != null && throwable.holderNetId == this.netId) 
            {
                throwable.Drop(Vector2.zero, this.netId, false); // Dropped, not thrown with force
                Debug.Log($"[PlayerInteraction CmdDrop] Player {netId} dropped object {heldObjectNetId}");
            }
        }
        heldObjectNetId = 0; 
    }

    [Command]
    void CmdThrowObject(Vector2 direction, float force)
    {
        Debug.Log($"[PlayerInteraction CmdThrowObject] Server received throw command. Object NetID: {heldObjectNetId}, Direction: {direction}, Force: {force}"); // LOG ADDED
        if (heldObjectNetId != 0 && NetworkServer.spawned.TryGetValue(heldObjectNetId, out NetworkIdentity objectIdentity))
        {
            ThrowableObject throwable = objectIdentity.GetComponent<ThrowableObject>();
            if (throwable != null && throwable.holderNetId == this.netId) 
            {
                throwable.Drop(direction * force, this.netId, true); 
                Debug.Log($"[PlayerInteraction CmdThrowObject] Called throwable.Drop for object {heldObjectNetId}");
            }
            else
            {
                Debug.LogWarning($"[PlayerInteraction CmdThrowObject] Throwable not found or not held by this player. Held: {throwable?.holderNetId}, This: {this.netId}");
            }
        }
        else
        {
            Debug.LogWarning($"[PlayerInteraction CmdThrowObject] heldObjectNetId is 0 or object not found in NetworkServer.spawned.");
        }
        heldObjectNetId = 0; 
    }

    // It's good practice to also update the local reference when the SyncVar changes
    // However, OnHolderChanged on ThrowableObject already handles parenting and physics.
    // We might need a hook for heldObjectNetId if we need to update 'currentHeldObject' reference on clients.
    // For now, the server commands directly manipulate the ThrowableObject.
    // REMOVED EXTRA BRACE that was here.

    void OnHeldObjectChanged(uint oldId, uint newId)
    {
        if (newId != 0)
        {
            if (NetworkClient.spawned.TryGetValue(newId, out NetworkIdentity objectIdentity))
            {
                currentHeldObject = objectIdentity.GetComponent<ThrowableObject>();
                if (currentHeldObject != null && isLocalPlayer) // If this client is holding it
                {
                    // The OnHolderChanged hook on ThrowableObject should handle parenting.
                    // This is just to ensure the local reference is set.
                    Debug.Log($"[PlayerInteraction Hook] Now holding {currentHeldObject.name}");
                    // We will force the position in LateUpdate if this is the local player holding it.
                }
            }
            else
            {
                Debug.LogError($"[PlayerInteraction Hook] Could not find spawned object with netId {newId}");
                currentHeldObject = null;
            }
        }
        else
        {
            Debug.Log($"[PlayerInteraction Hook] No longer holding an object.");
            currentHeldObject = null;
        }
    }

    void LateUpdate()
    {
        // If this local player is holding an object, ensure its position and rotation match the holdPoint.
        // This overrides NetworkTransform updates for the held object locally.
        if (isLocalPlayer && currentHeldObject != null && heldObjectNetId != 0)
        {
            if (currentHeldObject.holderNetId == this.netId) // Double check this player is the designated holder
            {
                currentHeldObject.transform.position = holdPoint.position;
                currentHeldObject.transform.rotation = holdPoint.rotation;
            }
        }
    }
}
