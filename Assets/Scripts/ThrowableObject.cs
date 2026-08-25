using Mirror;
using UnityEngine;

public class ThrowableObject : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHolderChanged))]
    public uint holderNetId = 0; 
    private uint throwerNetId = 0; // NetId of the player who threw this object, 0 if not thrown or picked up again

    public Rigidbody2D rb;
    public Collider2D col; // Main collider for physics

    private Transform originalParent;
    private bool originalKinematicState;
    // heldOffset is now determined by PlayerInteraction's holdPoint

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        originalParent = transform.parent; // In case it's parented in the scene initially
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // Ensure Rigidbody settings are appropriate for a dynamic object initially
        if (rb != null)
        {
            originalKinematicState = rb.isKinematic;
        }
    }

    void OnHolderChanged(uint oldHolderNetId, uint newHolderNetId)
    {
        Debug.Log($"[ThrowableObject:{netId}] Holder changed from {oldHolderNetId} to {newHolderNetId}");
        if (newHolderNetId != 0) // Picked up by someone
        {
            throwerNetId = 0; // Reset thrower when picked up by someone new
            if (NetworkServer.spawned.TryGetValue(newHolderNetId, out NetworkIdentity playerIdentity))
            {
                PlayerInteraction playerInteraction = playerIdentity.GetComponent<PlayerInteraction>();
                if (playerInteraction != null && playerInteraction.holdPoint != null)
                {
                    transform.SetParent(playerInteraction.holdPoint); 
                    transform.localPosition = Vector3.zero; 
                    transform.localRotation = Quaternion.identity; 
                    if (rb != null) rb.isKinematic = true;
                    if (col != null) col.enabled = false; 
                    Debug.Log($"[ThrowableObject:{netId}] Picked up by player {newHolderNetId}. Parented to {playerInteraction.holdPoint.name}. LocalPos: {transform.localPosition}");
                }
                else
                {
                    Debug.LogError($"[ThrowableObject:{netId}] Player {newHolderNetId} does not have PlayerInteraction or holdPoint. Attaching to player root as fallback.");
                    transform.SetParent(playerIdentity.transform); 
                    transform.localPosition = new Vector3(0.5f, 0, 0); 
                    if (rb != null) rb.isKinematic = true;
                    if (col != null) col.enabled = false;
                }
            }
            else
            {
                Debug.LogError($"[ThrowableObject:{netId}] Could not find player NetworkIdentity with netId {newHolderNetId} to attach to.");
                if (isServer) Drop(Vector3.zero, 0, true); // Force drop if player not found
            }
        }
        else // Dropped
        {
            // If it was just thrown, throwerNetId will be set. If picked up and then dropped by E, throwerNetId might be 0.
            // We only care about throwerNetId for collision after being thrown with force.
            transform.SetParent(originalParent); 
            if (rb != null) 
            {
                rb.isKinematic = originalKinematicState;
            }
            if (col != null) col.enabled = true;  
            Debug.Log($"[ThrowableObject:{netId}] Dropped. Thrower was: {throwerNetId}");
            // If not thrown with force (i.e., just dropped by E or max hold time), reset throwerNetId
            // This is handled in the Drop method now.
        }
    }

    [Server]
    public void Pickup(NetworkIdentity playerIdentity)
    {
        if (holderNetId == 0) 
        {
            holderNetId = playerIdentity.netId;
            throwerNetId = 0; // Reset thrower when picked up
            Debug.Log($"[ThrowableObject:{netId} Server] Pickup by player {playerIdentity.netId}. Thrower reset.");
        }
        else
        {
            Debug.LogWarning($"[ThrowableObject:{netId} Server] Pickup attempt failed. Already held by {holderNetId}.");
        }
    }

    [Server]
    public void Drop(Vector2 dropForce, uint playerWhoDroppedNetId, bool wasThrown = false) 
    {
        Debug.Log($"[ThrowableObject Drop SERVER ENTRY] Obj: {netId}, Dropper: {playerWhoDroppedNetId}, CurrentHolder: {holderNetId}, WasThrown: {wasThrown}, Force: {dropForce}"); // LOG ADDED
        if (holderNetId != 0 && holderNetId == playerWhoDroppedNetId) 
        {
            uint previousHolder = holderNetId;
            holderNetId = 0; // This assignment will trigger OnHolderChanged hook on clients
            
            if (wasThrown)
            {
                this.throwerNetId = previousHolder; 
            }
            else
            {
                this.throwerNetId = 0; 
            }

            if (rb != null)
            {
                rb.isKinematic = false; 
                Debug.Log($"[ThrowableObject Drop SERVER] Rigidbody isKinematic set to: {rb.isKinematic} for Obj: {netId}"); // LOG ADDED
                if (dropForce != Vector2.zero)
                {
                    rb.velocity = Vector2.zero; 
                    rb.angularVelocity = 0f;
                    rb.AddForce(dropForce, ForceMode2D.Impulse);
                    Debug.Log($"[ThrowableObject:{netId} Server] Thrown by player {previousHolder} (throwerNetId: {this.throwerNetId}) with force {dropForce}. Applied Velocity: {rb.velocity}");
                }
                else
                {
                    Debug.Log($"[ThrowableObject:{netId} Server] Dropped by player {previousHolder} (no force). Thrower set to 0.");
                }
            }
            else
            {
                Debug.LogError($"[ThrowableObject Drop SERVER] Rigidbody is NULL on object {netId}!"); // LOG ADDED
            }
        }
        else if (holderNetId != 0 && holderNetId != playerWhoDroppedNetId)
        {
            Debug.LogWarning($"[ThrowableObject:{netId} Server] Player {playerWhoDroppedNetId} tried to drop, but object is held by {holderNetId}.");
        }
        else if (holderNetId == 0)
        {
            Debug.LogWarning($"[ThrowableObject:{netId} Server] Drop called but object not currently held. Dropper: {playerWhoDroppedNetId}");
        }
    }

    [ServerCallback]
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (holderNetId != 0) return; 
        if (throwerNetId == 0) return; // Only apply collision effects if it was actively thrown by someone
        if (rb == null || rb.velocity.sqrMagnitude < 0.1f) return; 

        PlayerLobbyInfo hitPlayer = collision.gameObject.GetComponent<PlayerLobbyInfo>();
        if (hitPlayer != null)
        {
            // Check if the hit player is the one who threw it
            if (hitPlayer.netId == throwerNetId)
            {
                Debug.Log($"[ThrowableObject:{netId}] Collided with the thrower ({hitPlayer.playerNickname}). No stun applied.");
                // Optionally, make the object non-colliding with thrower for a short duration after throw
                // or simply let it bounce off.
            }
            else
            {
                Debug.Log($"[ThrowableObject:{netId}] Collided with player {hitPlayer.playerNickname} (thrower was {throwerNetId}). Applying stun.");
                hitPlayer.ApplyStun();
            }
            
            // Reset throwerNetId after first significant collision to prevent multiple stuns from one throw
            // or to allow it to be picked up again without old thrower context.
            throwerNetId = 0; 
            // NetworkServer.Destroy(gameObject); // Optional: Destroy object on impact
        }
    }
}
