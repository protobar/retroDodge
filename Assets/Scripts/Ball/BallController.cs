using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;

// ==================== BALL CONTROLLER ====================
public class BallController : MonoBehaviourPun, IPunObservable
{
    [Header("Ball Settings")]
    public float baseSpeed = 20f;
    public float gravity = -9.8f;
    public LayerMask playerLayer;
    public LayerMask groundLayer;

    [Header("Possession Settings")]
    public float possessionStartTime;
    public int currentHolderID = -1;

    // Ball state
    public enum BallType { Basic, Charged, Jump, Ultimate, Special }
    public enum BallState { Free, Held, Thrown, Caught }
    public BallType currentType;
    public BallState currentState;
    public int damage;
    public int ownerID;
    public bool isActive = true;

    // Custom physics
    private Vector3 velocity;
    private Vector3 curveDirection;
    private float curveIntensity;
    private TrailRenderer trail;

    // Visual indicators
    public GameObject warningGlow;
    public GameObject dangerGlow;
    public ParticleSystem corruptionParticles;

    // Network prediction
    private Queue<BallSnapshot> stateBuffer = new Queue<BallSnapshot>();
    private float lastNetworkUpdate = 0f;

    // Components
    private Rigidbody rb;
    private SphereCollider sphereCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        trail = GetComponent<TrailRenderer>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.2f;
        }
    }

    void Start()
    {
        ResetBall();
    }

    public void OnPickup(int playerID)
    {
        currentHolderID = playerID;
        currentState = BallState.Held;
        possessionStartTime = Time.time;

        // Disable physics while held
        velocity = Vector3.zero;
        rb.isKinematic = true;

        // Attach to player
        CharacterBase holder = FindPlayerByID(playerID);
        if (holder != null)
        {
            transform.SetParent(holder.transform);
            transform.localPosition = new Vector3(0, 1.5f, 0.5f);
        }

        if (photonView.IsMine)
        {
            photonView.RPC("SyncBallPickup", RpcTarget.Others, playerID, possessionStartTime);
        }
    }

    public void LaunchBall(Vector3 direction, float power, BallType type, int throwerId)
    {
        currentState = BallState.Thrown;
        currentType = type;
        ownerID = throwerId;
        currentHolderID = -1;

        // Detach from player
        transform.SetParent(null);
        rb.isKinematic = false;

        // Calculate velocity based on throw type
        velocity = direction.normalized * (baseSpeed * power);
        rb.velocity = velocity;

        // Apply throw-specific properties
        ApplyThrowProperties(type);

        isActive = true;

        if (photonView.IsMine)
        {
            photonView.RPC("SyncBallLaunch", RpcTarget.Others, velocity, type, throwerId);
        }
    }

    void ApplyThrowProperties(BallType type)
    {
        switch (type)
        {
            case BallType.Basic:
                damage = 10;
                trail.enabled = false;
                break;

            case BallType.Charged:
                damage = 15;
                trail.enabled = true;
                trail.startColor = Color.red;
                trail.endColor = Color.yellow;
                break;

            case BallType.Jump:
                damage = 12;
                velocity.y += 5f; // Extra upward force
                rb.velocity = velocity;
                break;

            case BallType.Ultimate:
                // Character-specific ultimate damage will be set by character
                trail.enabled = true;
                trail.startColor = Color.white;
                trail.endColor = Color.blue;
                break;

            case BallType.Special:
                // For character-specific special throws
                break;
        }
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine || !isActive) return;

        if (currentState == BallState.Thrown)
        {
            // Apply curve if needed (for Echo character)
            if (curveIntensity > 0)
            {
                Vector3 curveForce = curveDirection * curveIntensity * Time.fixedDeltaTime;
                rb.AddForce(curveForce);
            }

            // Check for collisions using raycast
            CheckCollisions();

            // Check if ball went out of bounds
            if (transform.position.y < -10f || Vector3.Distance(transform.position, Vector3.zero) > 50f)
            {
                ResetBall();
            }
        }
        else if (currentState == BallState.Held)
        {
            UpdatePossessionVisuals();
        }

        // Send position updates for network sync
        if (Time.time - lastNetworkUpdate > 0.1f)
        {
            photonView.RPC("SyncBallPosition", RpcTarget.Others, transform.position, rb.velocity);
            lastNetworkUpdate = Time.time;
        }
    }

    void CheckCollisions()
    {
        // Check for player collisions
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, sphereCollider.radius, playerLayer);

        foreach (var collider in hitColliders)
        {
            CharacterBase character = collider.GetComponent<CharacterBase>();
            if (character != null && character.photonView.Owner.ActorNumber != ownerID)
            {
                OnPlayerHit(character);
                return;
            }
        }

        // Check for ground collision
        if (Physics.CheckSphere(transform.position, sphereCollider.radius, groundLayer))
        {
            OnGroundHit();
        }
    }

    void OnPlayerHit(CharacterBase character)
    {
        // Check if player is trying to catch
        CatchController catchController = character.GetComponent<CatchController>();
        if (catchController != null && catchController.TryToCatch(this))
        {
            // Successful catch
            character.OnBallPickup();
            currentState = BallState.Caught;

            // Award ultimate charge to catcher
            UltimateManager ultimateManager = character.GetComponent<UltimateManager>();
            ultimateManager?.AddCharge(0.20f);

            AudioManager.Instance?.PlaySound("BallCatch");
        }
        else
        {
            // Hit player - deal damage
            character.TakeDamage(damage, currentType == BallType.Ultimate ? DamageType.Ultimate : DamageType.BallHit);

            // Award ultimate charge to thrower
            CharacterBase thrower = FindPlayerByID(ownerID);
            if (thrower != null)
            {
                UltimateManager throwerUltimate = thrower.GetComponent<UltimateManager>();
                throwerUltimate?.AddCharge(0.15f);
            }

            // Visual and audio feedback
            CreateHitEffect(transform.position);
            CameraShake.Instance?.Shake(0.3f, 0.4f);
            AudioManager.Instance?.PlaySound("BallHit");

            ResetBall();
        }
    }

    void OnGroundHit()
    {
        if (currentType == BallType.Jump)
        {
            // Special bounce behavior for jump throws
            Vector3 bounceVelocity = rb.velocity;
            bounceVelocity.y = Mathf.Abs(bounceVelocity.y) * 0.8f;
            rb.velocity = bounceVelocity;

            // Create ground impact effect
            CreateGroundImpactEffect();
            AudioManager.Instance?.PlaySound("GroundImpact");
        }
        else
        {
            // Normal ground hit - ball becomes inactive
            ResetBall();
        }
    }

    public float GetHoldDuration()
    {
        return Time.time - possessionStartTime;
    }

    public void UpdatePossessionVisuals()
    {
        float holdTime = GetHoldDuration();

        if (holdTime >= 5f)
        {
            if (dangerGlow != null) dangerGlow.SetActive(true);
            if (warningGlow != null) warningGlow.SetActive(false);
            if (corruptionParticles != null && !corruptionParticles.isPlaying)
            {
                corruptionParticles.Play();
            }
        }
        else if (holdTime >= 3f)
        {
            if (warningGlow != null) warningGlow.SetActive(true);
            if (dangerGlow != null) dangerGlow.SetActive(false);
            if (corruptionParticles != null && corruptionParticles.isPlaying)
            {
                corruptionParticles.Stop();
            }
        }
        else
        {
            if (warningGlow != null) warningGlow.SetActive(false);
            if (dangerGlow != null) dangerGlow.SetActive(false);
            if (corruptionParticles != null && corruptionParticles.isPlaying)
            {
                corruptionParticles.Stop();
            }
        }
    }

    public void SetCurve(Vector3 direction, float intensity)
    {
        curveDirection = direction;
        curveIntensity = intensity;
    }

    void ResetBall()
    {
        currentState = BallState.Free;
        currentHolderID = -1;
        ownerID = -1;
        isActive = false;
        velocity = Vector3.zero;

        if (rb != null) rb.velocity = Vector3.zero;
        if (trail != null) trail.enabled = false;

        // Reset visual effects
        if (warningGlow != null) warningGlow.SetActive(false);
        if (dangerGlow != null) dangerGlow.SetActive(false);
        if (corruptionParticles != null) corruptionParticles.Stop();

        // Return to pool or spawn new ball
        if (BallPool.Instance != null)
        {
            BallPool.Instance.ReturnBall(this);
        }

        // Spawn new ball after delay
        if (GameManager.Instance != null && PhotonNetwork.IsMasterClient)
        {
            GameManager.Instance.SpawnNewBall(1.5f);
        }
    }

    void CreateHitEffect(Vector3 position)
    {
        // Create particle effect at hit position
        if (EffectsPool.Instance != null)
        {
            GameObject hitEffect = EffectsPool.Instance.GetEffect("BallHit");
            if (hitEffect != null)
            {
                hitEffect.transform.position = position;
            }
        }
    }

    void CreateGroundImpactEffect()
    {
        if (EffectsPool.Instance != null)
        {
            GameObject impactEffect = EffectsPool.Instance.GetEffect("GroundImpact");
            if (impactEffect != null)
            {
                impactEffect.transform.position = transform.position;
            }
        }
    }

    CharacterBase FindPlayerByID(int playerID)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == playerID)
            {
                // Find the player's character in the scene
                GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
                foreach (var obj in playerObjects)
                {
                    PhotonView pv = obj.GetComponent<PhotonView>();
                    if (pv != null && pv.Owner.ActorNumber == playerID)
                    {
                        return obj.GetComponent<CharacterBase>();
                    }
                }
            }
        }
        return null;
    }

    // Network RPCs
    [PunRPC]
    void SyncBallPickup(int playerID, float pickupTime)
    {
        currentHolderID = playerID;
        possessionStartTime = pickupTime;
        currentState = BallState.Held;

        // Attach to player for non-owners
        CharacterBase holder = FindPlayerByID(playerID);
        if (holder != null)
        {
            transform.SetParent(holder.transform);
            transform.localPosition = new Vector3(0, 1.5f, 0.5f);
        }
    }

    [PunRPC]
    void SyncBallLaunch(Vector3 vel, BallType type, int throwerId)
    {
        transform.SetParent(null);
        velocity = vel;
        currentType = type;
        ownerID = throwerId;
        currentState = BallState.Thrown;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = vel;
        }

        ApplyThrowProperties(type);
    }

    [PunRPC]
    void SyncBallPosition(Vector3 pos, Vector3 vel)
    {
        // Interpolate for smooth movement on non-owner clients
        stateBuffer.Enqueue(new BallSnapshot
        {
            position = pos,
            velocity = vel,
            timestamp = Time.time
        });

        // Keep buffer size reasonable
        while (stateBuffer.Count > 10)
        {
            stateBuffer.Dequeue();
        }

        if (!photonView.IsMine && currentState == BallState.Thrown)
        {
            // Smooth interpolation to network position
            transform.position = Vector3.Lerp(transform.position, pos, Time.deltaTime * 15f);
            if (rb != null) rb.velocity = vel;
        }
    }

    // IPunObservable implementation
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send data
            stream.SendNext(transform.position);
            stream.SendNext(rb.velocity);
            stream.SendNext((int)currentState);
            stream.SendNext(currentHolderID);
        }
        else
        {
            // Receive data
            Vector3 networkPos = (Vector3)stream.ReceiveNext();
            Vector3 networkVel = (Vector3)stream.ReceiveNext();
            BallState networkState = (BallState)stream.ReceiveNext();
            int networkHolderID = (int)stream.ReceiveNext();

            if (!photonView.IsMine)
            {
                // Apply lag compensation
                float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
                networkPos += networkVel * lag;

                transform.position = Vector3.Lerp(transform.position, networkPos, Time.deltaTime * 20f);
                currentState = networkState;
                currentHolderID = networkHolderID;
            }
        }
    }
}

// ==================== BALL SNAPSHOT ====================
[System.Serializable]
public class BallSnapshot
{
    public Vector3 position;
    public Vector3 velocity;
    public float timestamp;
}