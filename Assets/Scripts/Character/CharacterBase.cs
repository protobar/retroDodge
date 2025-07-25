using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ==================== CHARACTER BASE CLASS ====================
// ==================== CHARACTER BASE CLASS ====================
public abstract class CharacterBase : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Character Settings")]
    public CharacterStats stats;

    // Components
    protected CharacterController characterController;
    protected StateMachine stateMachine;
    protected UltimateManager ultimateManager;
    protected BallPossessionManager possessionManager;

    // State
    public float currentHealth;
    public bool hasBall = false;
    protected bool isCharging = false;
    protected float chargeTime = 0f;
    protected float ballPickupTime = 0f;

    // Movement
    protected Vector3 moveDirection;
    protected float verticalVelocity = 0f;
    protected bool isGrounded = true;
    public bool canMove = true;

    // Network sync
    protected float lastSyncTime = 0f;
    protected Vector3 networkPosition;
    protected Quaternion networkRotation;
    protected Vector3 networkVelocity;

    // Quick throw bonus
    public bool eligibleForQuickThrowBonus = false;

    // Input
    protected float horizontal;
    protected bool jumpInput;
    protected bool throwInput;
    protected bool throwHeld;
    protected bool catchInput;
    protected bool dodgeInput;
    protected bool ultimateInput;

    // Abstract methods for character-specific implementation
    public abstract void OnBasicThrow();
    public abstract void OnChargedThrow();
    public abstract void OnJumpThrow();
    public abstract void OnUltimate();

    protected virtual void Awake()
    {
        characterController = GetComponent<CharacterController>();
        stateMachine = GetComponent<StateMachine>();
        ultimateManager = GetComponent<UltimateManager>();
        possessionManager = GetComponent<BallPossessionManager>();
    }

    protected virtual void Start()
    {
        currentHealth = stats.maxHealth;
        InitializeStateMachine();

        if (photonView.IsMine)
        {
            InputManager.Instance.RegisterPlayer(this);
        }

        // Register with GameManager
        GameManager.Instance.RegisterPlayer(this);
    }

    protected virtual void Update()
    {
        if (photonView.IsMine)
        {
            HandleInput();
            HandleMovement();
            HandleCharging();
        }
        else
        {
            // Network interpolation
            NetworkInterpolation();
        }
    }

    void HandleInput()
    {
        // Get input from InputManager
        horizontal = InputManager.GetHorizontal();
        jumpInput = InputManager.GetJump();
        throwInput = InputManager.GetThrow();
        throwHeld = InputManager.GetThrowHeld();
        catchInput = InputManager.GetCatch();
        dodgeInput = InputManager.GetDodge();
        ultimateInput = InputManager.GetUltimate();

        // Process inputs based on current state
        if (canMove)
        {
            if (jumpInput && isGrounded)
            {
                Jump();
            }

            if (dodgeInput)
            {
                Dodge();
            }
        }

        // Ball-related inputs
        if (hasBall)
        {
            if (throwInput && !isCharging)
            {
                StartCharging();
            }
            else if (!throwHeld && isCharging)
            {
                ExecuteThrow();
            }

            if (ultimateInput && ultimateManager.CanUseUltimate())
            {
                OnUltimate();
            }
        }
        else
        {
            if (catchInput)
            {
                TryToCatch();
            }
        }
    }

    void HandleMovement()
    {
        if (!canMove) return;

        // Horizontal movement
        moveDirection = new Vector3(horizontal * stats.movementSpeed, 0, 0);

        // Apply gravity
        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
        }

        moveDirection.y = verticalVelocity;

        // Move character
        characterController.Move(moveDirection * Time.deltaTime);

        // Check if grounded
        isGrounded = characterController.isGrounded;

        // Face movement direction
        if (horizontal != 0)
        {
            transform.rotation = Quaternion.LookRotation(horizontal > 0 ? Vector3.right : Vector3.left);
        }
    }

    void HandleCharging()
    {
        if (isCharging)
        {
            chargeTime += Time.deltaTime;

            // Visual feedback for charging
            UpdateChargingEffects();
        }
    }

    protected virtual void Jump()
    {
        verticalVelocity = Mathf.Sqrt(stats.jumpHeight * -2f * Physics.gravity.y);
        isGrounded = false;

        AudioManager.Instance?.PlaySound("Jump");
    }

    protected virtual void Dodge()
    {
        // Implement dodge mechanics
        stateMachine.ChangeState(StateType.Dodging);
        AudioManager.Instance?.PlaySound("Dodge");
    }

    void StartCharging()
    {
        isCharging = true;
        chargeTime = 0f;
        stateMachine.ChangeState(StateType.Charging);
    }

    void ExecuteThrow()
    {
        if (!hasBall) return;

        isCharging = false;

        // Determine throw type based on charge time and state
        if (ultimateManager.CanUseUltimate() && ultimateInput)
        {
            OnUltimate();
        }
        else if (!isGrounded)
        {
            OnJumpThrow();
        }
        else if (chargeTime >= 1.5f)
        {
            OnChargedThrow();
        }
        else
        {
            OnBasicThrow();
        }

        // Add ultimate charge for throwing
        float chargeAmount = eligibleForQuickThrowBonus ? 0.20f : 0.15f;
        ultimateManager.AddCharge(chargeAmount);

        chargeTime = 0f;
        ReleaseBall();
    }

    void TryToCatch()
    {
        // This will be handled by CatchController
    }

    public virtual void OnBallPickup()
    {
        hasBall = true;
        ballPickupTime = Time.time;
        eligibleForQuickThrowBonus = true;

        if (possessionManager != null)
        {
            possessionManager.StartPossessionTimer(this);
        }

        StartCoroutine(QuickThrowWindow());

        photonView.RPC("SyncBallPossession", RpcTarget.Others, true);
    }

    IEnumerator QuickThrowWindow()
    {
        yield return new WaitForSeconds(2f);
        eligibleForQuickThrowBonus = false;
    }

    public virtual void ReleaseBall()
    {
        hasBall = false;

        if (possessionManager != null)
        {
            possessionManager.StopPossessionTimer();
        }

        photonView.RPC("SyncBallPossession", RpcTarget.Others, false);
    }

    public virtual void TakeDamage(float damage, DamageType type)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, stats.maxHealth);

        // Different visual feedback based on damage type
        switch (type)
        {
            case DamageType.BallHit:
                ShowHitEffect();
                break;
            case DamageType.HoldPenalty:
                ShowHoldPenaltyEffect();
                break;
            case DamageType.Ultimate:
                ShowUltimateHitEffect();
                break;
        }

        // Add ultimate charge for taking damage
        ultimateManager.AddCharge(0.05f);

        // Enter hit state
        stateMachine.ChangeState(StateType.Hit);

        photonView.RPC("SyncHealth", RpcTarget.Others, currentHealth);

        // Update HUD
        HUDController.Instance?.UpdateHealthBar(photonView.Owner.ActorNumber, currentHealth);
    }

    public virtual void ResetHealth()
    {
        currentHealth = stats.maxHealth;
        photonView.RPC("SyncHealth", RpcTarget.Others, currentHealth);
        HUDController.Instance?.UpdateHealthBar(photonView.Owner.ActorNumber, currentHealth);
    }

    protected virtual void ShowHitEffect()
    {
        // Implement hit visual effects
        CameraShake.Instance?.Shake(0.2f, 0.3f);
    }

    protected virtual void ShowHoldPenaltyEffect()
    {
        // Implement hold penalty visual effects
    }

    protected virtual void ShowUltimateHitEffect()
    {
        // Implement ultimate hit visual effects
        CameraShake.Instance?.Shake(0.5f, 0.8f);
    }

    protected virtual void UpdateChargingEffects()
    {
        // Implement charging visual effects
        float chargePercent = Mathf.Clamp01(chargeTime / 1.5f);
        // Update particle effects, sound, etc.
    }

    protected virtual void InitializeStateMachine()
    {
        if (stateMachine != null)
        {
            stateMachine.Initialize(this);
        }
    }

    void NetworkInterpolation()
    {
        // Smooth network position interpolation
        if (Vector3.Distance(transform.position, networkPosition) > 5f)
        {
            transform.position = networkPosition;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * 15f);
        }

        transform.rotation = Quaternion.Lerp(transform.rotation, networkRotation, Time.deltaTime * 15f);
    }

    // Network RPCs
    [PunRPC]
    protected void SyncHealth(float health)
    {
        currentHealth = health;
        HUDController.Instance?.UpdateHealthBar(photonView.Owner.ActorNumber, health);
    }

    [PunRPC]
    protected void SyncBallPossession(bool ballPossession)
    {
        hasBall = ballPossession;
    }

    // IPunObservable implementation
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send data
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(moveDirection);
            stream.SendNext(currentHealth);
            stream.SendNext(hasBall);
        }
        else
        {
            // Receive data
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
            networkVelocity = (Vector3)stream.ReceiveNext();
            float networkHealth = (float)stream.ReceiveNext();
            bool networkBallPossession = (bool)stream.ReceiveNext();

            // Apply lag compensation
            float lag = Mathf.Abs((float)(PhotonNetwork.Time - info.SentServerTime));
            networkPosition += networkVelocity * lag;
        }
    }
}

// ==================== STATE MACHINE ====================
public enum StateType
{
    Idle,
    Moving,
    Jumping,
    Charging,
    Throwing,
    Dodging,
    Hit,
    Ultimate
}