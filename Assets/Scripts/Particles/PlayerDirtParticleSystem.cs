using Photon.Pun;
using UnityEngine;
using static UnityEngine.Mathf;

/// <inheritdoc />
/// <summary>
/// Creates dirt particles for player movement, with effects for walking, jumping, and landing
/// </summary>
public class PlayerDirtParticleSystem : MonoBehaviour
{
    [SerializeField] [Tooltip("Reference to the player GameObject")] private GameObject playerObject;

    [Header("Interaction Filters")]
    [SerializeField] [Tooltip("Particles won't spawn when player is colliding with objects on these layers")] private LayerMask excludedLayers;
    [SerializeField] [Tooltip("Radius to check for collisions with excluded objects")] private float collisionCheckRadius = 0.2f;

    [Header("Base Particle Settings")]
    [SerializeField] [Tooltip("Base emission rate when player is moving")] private float baseEmissionRate = 5f;
    [SerializeField] [Tooltip("Multiplier for emission rate when walking")] private float walkingEmissionMultiplier = 2f;
    [SerializeField] [Tooltip("Minimum lifetime of particles in seconds")] private float particleLifetimeMin = 0.5f;
    [SerializeField] [Tooltip("Maximum lifetime of particles in seconds")] private float particleLifetimeMax = 1.0f;
    [SerializeField] [Tooltip("Minimum size of individual particles")] private float particleSizeMin = 0.05f;
    [SerializeField] [Tooltip("Maximum size of individual particles")] private float particleSizeMax = 0.15f;
    [SerializeField] [Tooltip("How strongly gravity affects the particles")] private float gravityModifier = 0.5f;
    [SerializeField] [Tooltip("Minimum color variation for dirt particles")] private Color dirtColorMin = new(0.6f, 0.4f, 0.2f, 0.7f);
    [SerializeField] [Tooltip("Maximum color variation for dirt particles")] private Color dirtColorMax = new(0.7f, 0.5f, 0.3f, 0.5f);

    [Header("Jump Particles")]
    [SerializeField] [Tooltip("Whether to emit particles when player jumps")] private bool enableJumpParticles = true;
    [SerializeField] [Tooltip("Minimum number of particles emitted on jump")] private float jumpBurstCountMin = 5f;
    [SerializeField] [Tooltip("Maximum number of particles emitted on jump")] private float jumpBurstCountMax = 20f;
    [SerializeField] [Tooltip("Minimum speed of particles emitted on jump")] private float jumpBurstSpeedMin = 1f;
    [SerializeField] [Tooltip("Maximum speed of particles emitted on jump")] private float jumpBurstSpeedMax = 3f;

    [Header("Landing Particles")]
    [SerializeField] [Tooltip("Whether to emit particles when player lands")] private bool enableLandingParticles = true;
    [SerializeField] [Tooltip("Minimum number of particles emitted on landing")] private float landingBurstCountMin = 3f;
    [SerializeField] [Tooltip("Maximum number of particles emitted on landing")] private float landingBurstCountMax = 15f;
    [SerializeField] [Tooltip("Minimum speed of particles emitted on landing")] private float landingBurstSpeedMin = 0.7f;
    [SerializeField] [Tooltip("Maximum speed of particles emitted on landing")] private float landingBurstSpeedMax = 2.1f;
    [SerializeField] [Tooltip("Minimum falling force required to trigger landing particles")] private float minimumLandingForce = 0.5f;
    [SerializeField] [Tooltip("Maximum falling force for scaling landing particles")] private float maximumLandingForce = 10f;

    [Header("Rendering")]
    [SerializeField] [Tooltip("Sorting layer for particle renderer")] private string sortingLayerName = "Foreground";
    [SerializeField] [Tooltip("Sorting order within the layer")] private int sortingOrder = 10;

    [Header("Performance")]
    [SerializeField] [Tooltip("Maximum number of particles allowed at once")] private int maxParticles = 100;

    [Header("Remote Player Settings")]
    [SerializeField] [Tooltip("Reduces opacity of particles for remote players")] private float remotePlayerOpacityMultiplier = 0.7f;

    private PlayerController _playerController;
    private ParticleSystem _dirtParticleSystem;
    private ParticleSystem.EmissionModule _emission;
    private ParticleSystem.MainModule _main;
    private PhotonView _photonView;
    private Material _particleMaterial;

    private bool _wasGrounded;
    private float _lastFallingSpeed;
    private float _inverseJumpForceRange;
    private float _inverseLandingForceRange;
    private float _inverseMaxSpeed;

    /// Creates the particle system and initializes basic settings
    private void Awake()
    {
        _dirtParticleSystem = gameObject.AddComponent<ParticleSystem>();
        _emission = _dirtParticleSystem.emission;
        _main = _dirtParticleSystem.main;

        _particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));

        SetupParticleSystem();
    }

    /// Initializes references and calculated values
    private void Start()
    {
        if (playerObject == null)
        {
            Debug.LogError("Player object reference is missing!");
            enabled = false;
            return;
        }

        _playerController = playerObject.GetComponent<PlayerController>();
        if (_playerController == null)
        {
            Debug.LogError("PlayerController component not found on player object!");
            enabled = false;
            return;
        }

        _photonView = playerObject.GetComponent<PhotonView>();
        _wasGrounded = _playerController.IsGrounded;

        _inverseJumpForceRange = 1f / (_playerController.JumpSystem.maxJumpForce - _playerController.JumpSystem.minJumpForce);
        _inverseLandingForceRange = 1f / (maximumLandingForce - minimumLandingForce);
        _inverseMaxSpeed = 1f / _playerController.maxSpeed;

        if (_photonView && !_photonView.IsMine)
            AdjustForRemotePlayer();
    }

    /// Cleans up created materials when destroyed
    private void OnDestroy()
    {
        if (_particleMaterial != null)
            Destroy(_particleMaterial);
    }

    /// Manages particle emission based on player state and movement
    private void Update()
    {
        if (!_playerController)
            return;

        if (_playerController.IsDead || IsCollidingWithExcludedObjects())
        {
            if (_emission.rateOverTime.constant > 0)
                _emission.rateOverTime = 0;
            return;
        }

        var isGrounded = _playerController.IsGrounded;
        var verticalSpeed = _playerController.VerticalSpeed;

        if (!isGrounded && verticalSpeed < 0)
            _lastFallingSpeed = Abs(verticalSpeed);

        if (_wasGrounded && !isGrounded && verticalSpeed > 0)
            if (enableJumpParticles)
                EmitJumpParticles(verticalSpeed);

        if (isGrounded && !_wasGrounded)
            if (enableLandingParticles)
                EmitLandingParticles(_lastFallingSpeed);

        if (isGrounded)
        {
            var currentSpeed = Abs(_playerController.CurrentSpeed);
            UpdateWalkingParticles(currentSpeed);
        }
        else if (_emission.rateOverTime.constant > 0)
        {
            _emission.rateOverTime = 0;
        }

        _wasGrounded = isGrounded;
    }

    /// Checks if player is touching excluded objects that should prevent particles
    private bool IsCollidingWithExcludedObjects()
    {
        if (excludedLayers.value == 0) return false;

        var layerHit = Physics2D.OverlapCircle(
            playerObject.transform.position,
            collisionCheckRadius,
            excludedLayers);

        return layerHit && layerHit.gameObject != playerObject;
    }

    /// Configures the particle system with all necessary modules and settings
    private void SetupParticleSystem()
    {
        _main.startColor = new ParticleSystem.MinMaxGradient(dirtColorMin, dirtColorMax);
        _main.startSize = new ParticleSystem.MinMaxCurve(particleSizeMin, particleSizeMax);
        _main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetimeMin, particleLifetimeMax);
        _main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);
        _main.simulationSpace = ParticleSystemSimulationSpace.World;
        _main.gravityModifier = gravityModifier;
        _main.maxParticles = maxParticles;

        _emission.rateOverTime = 0;

        var shape = _dirtParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        shape.radiusThickness = 0;

        var colorOverLifetime = _dirtParticleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new(Color.white, 0.0f), new(Color.white, 1.0f) },
            new GradientAlphaKey[] { new(1.0f, 0.0f), new(0.0f, 1.0f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = _dirtParticleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        SetupParticleRenderer();
    }

    /// Configures the particle system renderer component
    private void SetupParticleRenderer()
    {
        var component = _dirtParticleSystem.GetComponent<ParticleSystemRenderer>();
        if (component == null) return;

        component.sortingLayerName = sortingLayerName;
        component.sortingOrder = sortingOrder;

        if (_particleMaterial != null)
        {
            component.material = _particleMaterial;
            component.trailMaterial = _particleMaterial;
        }
        else
        {
            Debug.LogWarning("Could not find 'Particles/Standard Unlit' shader. Using default material.");
        }

        component.renderMode = ParticleSystemRenderMode.Billboard;
        component.alignment = ParticleSystemRenderSpace.View;
    }

    /// Manages particle emission rate based on player's walking speed
    private void UpdateWalkingParticles(float speed)
    {
        if (speed > 0.1f)
        {
            var speedFactor = Clamp01(speed * _inverseMaxSpeed);
            _emission.rateOverTime = baseEmissionRate * speedFactor * walkingEmissionMultiplier;
        }
        else if (_emission.rateOverTime.constant > 0)
        {
            _emission.rateOverTime = 0;
        }
    }

    /// Creates burst of particles when player jumps
    private void EmitJumpParticles(float jumpForce)
    {
        var jumpForceFactor = Clamp01(
            (jumpForce - _playerController.JumpSystem.minJumpForce) * _inverseJumpForceRange
        );

        var burstCount = RoundToInt(Lerp(jumpBurstCountMin, jumpBurstCountMax, jumpForceFactor));
        var burstSpeed = Lerp(jumpBurstSpeedMin, jumpBurstSpeedMax, jumpForceFactor);

        _main.startSpeed = new ParticleSystem.MinMaxCurve(burstSpeed * 0.5f, burstSpeed);
        _dirtParticleSystem.Emit(burstCount);
    }

    /// Creates burst of particles when player lands
    private void EmitLandingParticles(float landingForce)
    {
        if (landingForce < minimumLandingForce)
            return;

        var landingForceFactor = Clamp01(
            (landingForce - minimumLandingForce) * _inverseLandingForceRange
        );

        var burstCount = RoundToInt(Lerp(landingBurstCountMin, landingBurstCountMax, landingForceFactor));
        var burstSpeed = Lerp(landingBurstSpeedMin, landingBurstSpeedMax, landingForceFactor);

        _main.startSpeed = new ParticleSystem.MinMaxCurve(burstSpeed * 0.3f, burstSpeed);
        _dirtParticleSystem.Emit(burstCount);
    }

    /// Reduces particle opacity for networked players that aren't the local player
    private void AdjustForRemotePlayer()
    {
        var startColor = _main.startColor;
        var minColor = startColor.colorMin;
        var maxColor = startColor.colorMax;

        minColor.a *= remotePlayerOpacityMultiplier;
        maxColor.a *= remotePlayerOpacityMultiplier;

        _main.startColor = new ParticleSystem.MinMaxGradient(minColor, maxColor);
    }
}
