using Mirror;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 movementInput;
    private PlayerLobbyInfo playerInfo; 
    private Vector3 initialNicknameLocalScale; // To store the original local scale of the nickname

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("[PlayerMovement] Rigidbody2D component not found!", this);
            enabled = false; 
            return;
        }
        
        playerInfo = GetComponent<PlayerLobbyInfo>();
        if (playerInfo == null)
        {
            Debug.LogError("[PlayerMovement] PlayerLobbyInfo component not found!", this);
        }
        else if (playerInfo.nicknameDisplayText != null)
        {
            // Store the initial local scale of the nickname display text
            initialNicknameLocalScale = playerInfo.nicknameDisplayText.transform.localScale;
        }


        if (Camera.main != null)
        {
            SmoothCameraFollow cameraFollow = Camera.main.GetComponent<SmoothCameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform);
            }
            else
            {
                Debug.LogWarning("[PlayerMovement] SmoothCameraFollow script not found on Main Camera.");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerMovement] Main Camera not found.");
        }
    }

    void Update()
    {
        if (!isLocalPlayer || (playerInfo != null && playerInfo.isStunned))
        {
            movementInput = Vector2.zero; 
            return;
        }
        movementInput.x = Input.GetAxisRaw("Horizontal");
        movementInput.y = Input.GetAxisRaw("Vertical");
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer || rb == null) return;

        if (playerInfo != null && playerInfo.isStunned)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (movementInput.sqrMagnitude > 1) movementInput.Normalize();
        rb.velocity = movementInput * moveSpeed;

        // Flip player sprite
        if (movementInput.x > 0.01f) // Moving Right
        {
            if (transform.localScale.x < 0f) // If currently flipped left
            {
                Vector3 newScale = transform.localScale;
                newScale.x *= -1;
                transform.localScale = newScale;
            }
        }
        else if (movementInput.x < -0.01f) // Moving Left
        {
            if (transform.localScale.x > 0f) // If currently facing right
            {
                Vector3 newScale = transform.localScale;
                newScale.x *= -1;
                transform.localScale = newScale;
            }
        }

        // Adjust nickname display scale to counteract parent's flip
        if (playerInfo != null && playerInfo.nicknameDisplayText != null)
        {
            // We want the nickname's world orientation to remain consistent.
            // If the parent (player) flips its x-scale, the child (nickname) also flips.
            // To counteract this, we flip the child's localScale.x again if the parent is flipped.
            // This means if parent.localScale.x is -1, child.localScale.x should also be -1 (relative to its original orientation).
            
            Vector3 currentNicknameScale = playerInfo.nicknameDisplayText.transform.localScale;
            float desiredNicknameScaleX = initialNicknameLocalScale.x; // Start with original orientation

            if (transform.localScale.x < 0) // If player is flipped (facing left, assuming default is right)
            {
                // To make the text appear normal (not mirrored), its local scale X should also be negative
                // if its initial local scale X was positive.
                desiredNicknameScaleX = Mathf.Abs(initialNicknameLocalScale.x) * -1f;
            }
            else // Player is not flipped (facing right)
            {
                desiredNicknameScaleX = Mathf.Abs(initialNicknameLocalScale.x);
            }

            if (Mathf.Abs(currentNicknameScale.x - desiredNicknameScaleX) > 0.001f) // Avoid tiny float comparison issues
            {
                 playerInfo.nicknameDisplayText.transform.localScale = new Vector3(
                    desiredNicknameScaleX, 
                    initialNicknameLocalScale.y, 
                    initialNicknameLocalScale.z
                );
            }
        }
    }
}
