using UnityEngine;

public class Crop : MonoBehaviour
{
    [Header("Base Settings")]
    [Range(0f, 1f)] public float baseCorruptionRate = 0.1f;
    public float growTime = 5f;
    public Renderer leavesRenderer;

    public enum CropState { Seed, Normal, Affected, Corrupted }
    public CropState state = CropState.Seed;

    // ---------------- GROWTH ----------------
    private bool hasGrown = false;
    private float timer = 0f;

    // ---------------- INFECTION ----------------
    public float infectionProgress = 0f;
    public float infectionThreshold = 1f;
    public float infectionStrength = 0f;

    public float localCorruption = 0f;


    // ---------------- VEINS ----------------    
    public CropData data;

    public GameObject veinPlanePrefab;
    private GameObject infectionVisual;
    private Material _veinMaterial;

    private float _crawlRadius = 0f;
    private bool _hasSpread = false;

    // ---------------- VISUAL ----------------
    private Color originalColor;

    // ---------------- SHAKE ----------------
    private bool isShaking = false;
    private float shakeTimer = 0f;
    private float shakeDuration = 0.3f;
    private float shakeStrength = 0.05f;

    private Vector3 basePosition;
    private Collider[] _colliders;

    // Cached references (important)
    private PlayerInteraction player;

    // =========================================================
    // START
    // =========================================================
    void Start()
    {
        player = FindAnyObjectByType<PlayerInteraction>();

        localCorruption = Random.Range(-0.1f, 0.1f);

        if (leavesRenderer != null)
        {
            leavesRenderer.material = new Material(leavesRenderer.material);
            originalColor = data.healthyColor;
        }

        basePosition = transform.position;
        _colliders = GetComponentsInChildren<Collider>();

        state = CropState.Seed;
        ApplyState();
    }

    // =========================================================
    // UPDATE
    // =========================================================
    void Update()
    {
        timer += Time.deltaTime;

        if (!hasGrown && timer >= growTime)
        {
            hasGrown = true;
            Grow();
        }

        if (hasGrown)
        {
            HandleExposureProgression();
        }

        if (state == CropState.Corrupted)
        {
            UpdateCorruptionSpread();
        }

        UpdateVisualInfectionGlow();
        UpdateShake();
        UpdateVisuals();
    }

    // =========================================================
    // GROW
    // =========================================================
    void Grow()
    {
        float corruptionChance =
            data.baseCorruptionRate +
            (player != null ? player.corruption : 0f) +
            localCorruption;

        if (Random.value < corruptionChance)
        {
            SetCorrupted();
        }
        else
        {
            state = CropState.Normal;
        }

        ApplyState();
    }

    // =========================================================
    // CORE CORRUPTION
    // =========================================================
    void SetCorrupted()
    {
        if (state == CropState.Corrupted)
            return;

        state = CropState.Corrupted;

        infectionStrength = Random.Range(0.3f, 1f);

        SpawnVeinPlane(); // ALWAYS spawn

        ApplyState();
        StartShake();
    }

    // =========================================================
    // SPREAD SYSTEM
    // =========================================================
    void UpdateCorruptionSpread()
    {
        infectionStrength += Time.deltaTime * 0.05f;
        infectionStrength = Mathf.Clamp01(infectionStrength);

        if (_crawlRadius < data.visualRadius)
        {
            _crawlRadius += Time.deltaTime * data.crawlSpeed;
            _crawlRadius = Mathf.Min(_crawlRadius, data.visualRadius);
        }

        if (!_hasSpread)
            SpreadToNearby();
    }

    void SpreadToNearby()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, _crawlRadius);

        foreach (Collider col in nearby)
        {
            if (col.gameObject == gameObject) continue;

            Crop other = col.GetComponent<Crop>();
            if (other == null || other.state == CropState.Corrupted) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            if (dist <= _crawlRadius)
            {
                other.AddInfection(Time.deltaTime * infectionStrength);
            }
        }

        if (_crawlRadius >= data.visualRadius)
            _hasSpread = true;
    }

    public void AddInfection(float amount)
    {
        if (state == CropState.Corrupted)
            return;

        infectionProgress += amount / data.infectionResistance;

        if (infectionProgress >= infectionThreshold)
        {
            SetCorrupted();
        }
    }

    // =========================================================
    // EXPOSURE (PASSIVE INFECTION)
    // =========================================================
    void HandleExposureProgression()
    {
        if (state == CropState.Corrupted) return;

        bool nearCorruption = CheckNearbyCorruption();

        float rate = nearCorruption ? 1f : -0.5f;

        infectionProgress += rate * Time.deltaTime;
        infectionProgress = Mathf.Clamp(infectionProgress, 0f, infectionThreshold);

        if (infectionProgress >= infectionThreshold)
        {
            SetCorrupted();
        }
    }

    bool CheckNearbyCorruption()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, data.infectionRadius);

        foreach (Collider col in nearby)
        {
            Crop c = col.GetComponent<Crop>();
            if (c != null && c != this && c.state == CropState.Corrupted)
                return true;
        }

        return false;
    }

    // =========================================================
    // VEIN VISUALS
    // =========================================================
    void SpawnVeinPlane()
    {
        if (veinPlanePrefab == null) return;

        infectionVisual = Instantiate(
            veinPlanePrefab,
            transform.position + Vector3.up * 0.15f, // ✅ FIXED HEIGHT
            Quaternion.Euler(90, 0, 0)
        );

        Renderer rend = infectionVisual.GetComponent<Renderer>();

        if (rend != null)
        {
            _veinMaterial = new Material(rend.material);
            rend.material = _veinMaterial;

            _veinMaterial.SetFloat("_MaxRadius", data.visualRadius);
        }
    }

    void UpdateVisuals()
    {
        if (infectionVisual == null || state != CropState.Corrupted)
            return;

        infectionVisual.transform.position = basePosition + Vector3.up * 0.15f;

        float diameter = data.visualRadius * 2f;
        infectionVisual.transform.localScale = new Vector3(diameter, diameter, 0.05f);

        if (_veinMaterial != null)
        {
            _veinMaterial.SetFloat("_GrowProgress", infectionStrength);
            _veinMaterial.SetFloat("_CrawlRadius", _crawlRadius);
        }
    }

    // =========================================================
    // VISUAL FEEDBACK
    // =========================================================
    void UpdateVisualInfectionGlow()
    {
        if (state == CropState.Corrupted || leavesRenderer == null)
            return;

        float t = infectionProgress / infectionThreshold;
        if (t <= 0f) return;

        Color sick = data.sickColor;
        Color corruptHint = data.corruptedColor;

        Color target = t < 0.5f
            ? Color.Lerp(originalColor, sick, t * 2f)
            : Color.Lerp(sick, corruptHint, (t - 0.5f) * 2f);

        leavesRenderer.material.color = target;
    }

    // =========================================================
    // SHAKE
    // =========================================================
    void StartShake()
    {
        isShaking = true;
        shakeTimer = shakeDuration;
    }

    void UpdateShake()
    {
        if (!isShaking) return;

        shakeTimer -= Time.deltaTime;

        if (shakeTimer <= 0f)
        {
            isShaking = false;
            transform.position = basePosition;
            return;
        }

        float strength = shakeStrength * (shakeTimer / shakeDuration);

        transform.position = basePosition + new Vector3(
            Random.Range(-strength, strength),
            0,
            Random.Range(-strength, strength)
        );
    }

    public bool CanHarvest() => hasGrown;

    // =========================================================
    // APPLY STATE
    // =========================================================
    void ApplyState()
    {
        bool enableCollision = state != CropState.Seed;

        if (_colliders != null)
        {
            foreach (var col in _colliders)
                col.enabled = enableCollision;
        }

        switch (state)
        {
            case CropState.Seed:
                transform.localScale = new Vector3(0.1f, 0.3f, 0.1f);
                break;

            case CropState.Normal:
                transform.localScale = Vector3.one;
                break;

            case CropState.Affected:
                transform.localScale = new Vector3(1f, 1.3f, 1f);
                break;

            case CropState.Corrupted:
                transform.localScale = Vector3.one * 1.2f;

                if (leavesRenderer != null)
                    leavesRenderer.material.color = new Color(0.6f, 0f, 0.8f);

                break;
        }
    }
}