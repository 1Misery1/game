using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SolarSystemRuntime : MonoBehaviour
{
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private readonly List<OrbitMotion> orbits = new List<OrbitMotion>();
    private readonly List<SelfRotation> spins = new List<SelfRotation>();

    private Camera mainCamera;
    private Transform sun;
    private Transform earth;
    private Light sunLight;

    private Vector3 defaultCameraPosition;
    private Quaternion defaultCameraRotation;
    private float mainViewDistance = 18.0f;

    private Transform focusTarget;
    private float focusDistance;
    private float currentFocusDistance;
    private float focusHeight;
    private string focusFact;

    private Canvas canvas;
    private Text factText;
    private Button returnButton;
    private AudioSource audioSource;

    private const float CameraMoveSpeed = 5.0f;
    private const float CameraRotateSpeed = 6.0f;
    private const float ScrollZoomSpeed = 8.0f;
    private const float MinMainViewDistance = 6.0f;
    private const float MaxMainViewDistance = 40.0f;
    private const float MinFocusDistance = 0.8f;
    private const float MaxFocusDistance = 25.0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Object.FindObjectOfType<SolarSystemRuntime>() != null)
        {
            return;
        }

        var root = new GameObject("SolarSystem_Root");
        root.AddComponent<SolarSystemRuntime>();
        Debug.Log("[SolarSystemRuntime] Bootstrap created SolarSystem_Root.");
    }

    private void Start()
    {
        Debug.Log("[SolarSystemRuntime] Start.");
        ConfigureCamera();
        ConfigureLighting();
        BuildUI();
        BuildAudio();
        BuildBodies();
    }

    private void Update()
    {
        for (int i = 0; i < orbits.Count; i++)
        {
            orbits[i].Tick(Time.deltaTime);
        }

        for (int i = 0; i < spins.Count; i++)
        {
            spins[i].Tick(Time.deltaTime);
        }

        HandleScrollZoom();
        HandleClick();
    }

    private void LateUpdate()
    {
        UpdateCameraTransition(Time.deltaTime);
    }

    private void ConfigureCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var cameraObj = new GameObject("Main Camera");
            mainCamera = cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";
        }

        defaultCameraPosition = new Vector3(0.0f, 5.0f, -mainViewDistance);
        mainCamera.transform.position = defaultCameraPosition;
        mainCamera.transform.LookAt(Vector3.zero);
        defaultCameraRotation = mainCamera.transform.rotation;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.01f, 0.01f, 0.04f);
        mainCamera.fieldOfView = 60.0f;

        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }

    private void ConfigureLighting()
    {
        sunLight = new GameObject("Sun Light").AddComponent<Light>();
        sunLight.type = LightType.Point;
        sunLight.range = 100.0f;
        sunLight.intensity = 2.2f;
        sunLight.color = new Color(1.0f, 0.95f, 0.75f);
        sunLight.transform.position = Vector3.zero;

        var ambient = RenderSettings.ambientLight;
        if (ambient.maxColorComponent < 0.08f)
        {
            RenderSettings.ambientLight = new Color(0.06f, 0.06f, 0.08f);
        }
    }

    private void BuildBodies()
    {
        sun = CreateSphere("Sun", null, 2.0f, Vector3.zero, transform);
        var sunRenderer = sun.GetComponent<Renderer>();
        sunRenderer.sharedMaterial = CreateSunMaterial();
        spins.Add(new SelfRotation(sun, 8.0f));
        sun.gameObject.AddComponent<CelestialBody>().Initialize("Sun", "The Sun is a star. It gives us light and warmth.", 9.0f, 2.5f);

        var mercury = CreateOrbitingPlanet(
            bodyName: "Mercury",
            textureName: "2k_mercury",
            scale: 0.2f,
            orbitRadius: 4.0f,
            orbitSpeed: 38.0f,
            spinSpeed: 18.0f,
            fact: "Mercury is the closest planet to the Sun."
        );

        var venus = CreateOrbitingPlanet(
            bodyName: "Venus",
            textureName: "2k_venus_surface",
            scale: 0.35f,
            orbitRadius: 6.0f,
            orbitSpeed: 28.0f,
            spinSpeed: 10.0f,
            fact: "Venus is very hot and covered by thick clouds."
        );

        CreateVenusAtmosphere(venus, 1.02f);

        earth = CreateOrbitingPlanet(
            bodyName: "Earth",
            textureName: "2k_earth_daymap",
            scale: 0.37f,
            orbitRadius: 8.0f,
            orbitSpeed: 22.0f,
            spinSpeed: 30.0f,
            fact: "Earth is our home planet with air and water."
        );

        CreateMoon(earth, 0.1f, 1.0f, 95.0f, 12.0f);

        CreateOrbitingPlanet(
            bodyName: "Mars",
            textureName: "2k_mars",
            scale: 0.30f,
            orbitRadius: 10.0f,
            orbitSpeed: 18.0f,
            spinSpeed: 24.0f,
            fact: "Mars is called the red planet."
        );

        SelectBody(earth.GetComponent<CelestialBody>(), playSound: false);
    }

    private void BuildUI()
    {
        var canvasObj = new GameObject("UI_Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        var panelObj = new GameObject("FactPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.02f, 0.02f);
        panelRect.anchorMax = new Vector2(0.62f, 0.2f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0.0f, 0.0f, 0.0f, 0.58f);

        var factObj = new GameObject("FactText");
        factObj.transform.SetParent(panelObj.transform, false);
        factText = factObj.AddComponent<Text>();
        factText.font = GetBuiltinFont();
        factText.fontSize = 24;
        factText.alignment = TextAnchor.MiddleLeft;
        factText.horizontalOverflow = HorizontalWrapMode.Wrap;
        factText.verticalOverflow = VerticalWrapMode.Overflow;
        factText.color = new Color(0.92f, 0.95f, 1.0f, 1.0f);
        var factRect = factObj.GetComponent<RectTransform>();
        factRect.anchorMin = new Vector2(0.03f, 0.12f);
        factRect.anchorMax = new Vector2(0.97f, 0.88f);
        factRect.offsetMin = Vector2.zero;
        factRect.offsetMax = Vector2.zero;

        var buttonObj = new GameObject("ReturnButton");
        buttonObj.transform.SetParent(canvasObj.transform, false);
        returnButton = buttonObj.AddComponent<Button>();
        var buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.12f, 0.2f, 0.38f, 0.9f);
        var buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.79f, 0.9f);
        buttonRect.anchorMax = new Vector2(0.98f, 0.98f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        var buttonTextObj = new GameObject("Text");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        var buttonText = buttonTextObj.AddComponent<Text>();
        buttonText.font = GetBuiltinFont();
        buttonText.fontSize = 24;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.text = "Return Main View";
        var buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;

        returnButton.onClick.AddListener(ReturnToMainView);
    }

    private void BuildAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.0f;
        audioSource.volume = 0.35f;
        audioSource.clip = CreateClickTone();
    }

    private Transform CreateOrbitingPlanet(string bodyName, string textureName, float scale, float orbitRadius, float orbitSpeed, float spinSpeed, string fact)
    {
        var orbitPivot = new GameObject("Orbit_" + bodyName).transform;
        orbitPivot.SetParent(transform, false);
        orbitPivot.localPosition = Vector3.zero;

        var planet = CreateSphere(bodyName, textureName, scale, new Vector3(orbitRadius, 0.0f, 0.0f), orbitPivot);
        orbits.Add(new OrbitMotion(orbitPivot, orbitSpeed));
        spins.Add(new SelfRotation(planet, spinSpeed));
        planet.gameObject.AddComponent<CelestialBody>().Initialize(bodyName, fact, Mathf.Max(scale * 9.0f, 3.2f), Mathf.Max(scale * 2.0f, 0.6f));
        return planet;
    }

    private void CreateMoon(Transform earthTransform, float scale, float orbitRadius, float orbitSpeed, float spinSpeed)
    {
        var moonOrbit = new GameObject("Orbit_Moon").transform;
        moonOrbit.SetParent(earthTransform, false);
        moonOrbit.localPosition = Vector3.zero;

        var moon = CreateSphere("Moon", null, scale, new Vector3(orbitRadius, 0.0f, 0.0f), moonOrbit);
        moon.GetComponent<Renderer>().sharedMaterial = CreateFallbackMaterial("Moon_Mat", new Color(0.64f, 0.66f, 0.71f));
        orbits.Add(new OrbitMotion(moonOrbit, orbitSpeed));
        spins.Add(new SelfRotation(moon, spinSpeed));
        moon.gameObject.AddComponent<CelestialBody>().Initialize("Moon", "The Moon goes around Earth.", 2.8f, 0.55f);
    }

    private void CreateVenusAtmosphere(Transform venusTransform, float scaleMultiplier)
    {
        var atmosphere = CreateSphere("Venus_Atmosphere", "2k_venus_atmosphere", venusTransform.localScale.x * scaleMultiplier, Vector3.zero, venusTransform);
        atmosphere.GetComponent<Collider>().enabled = false;

        var atmosphereMat = atmosphere.GetComponent<Renderer>().material;
        atmosphereMat.SetFloat("_Mode", 3f);
        atmosphereMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        atmosphereMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        atmosphereMat.SetInt("_ZWrite", 0);
        atmosphereMat.DisableKeyword("_ALPHATEST_ON");
        atmosphereMat.EnableKeyword("_ALPHABLEND_ON");
        atmosphereMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        atmosphereMat.renderQueue = 3000;

        var color = atmosphereMat.color;
        color.a = 0.45f;
        atmosphereMat.color = color;
        spins.Add(new SelfRotation(atmosphere, 18.0f));
    }

    private Transform CreateSphere(string objectName, string textureName, float scale, Vector3 localPosition, Transform parent)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = objectName;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = Vector3.one * scale;

        if (!string.IsNullOrEmpty(textureName))
        {
            obj.GetComponent<Renderer>().sharedMaterial = CreatePlanetMaterial(textureName);
        }
        else
        {
            obj.GetComponent<Renderer>().sharedMaterial = CreateFallbackMaterial(objectName + "_Mat", new Color(0.75f, 0.75f, 0.75f));
        }

        return obj.transform;
    }

    private Material CreatePlanetMaterial(string textureName)
    {
        if (materials.TryGetValue(textureName, out var existing))
        {
            return existing;
        }

        var texture = Resources.Load<Texture2D>("Textures/" + textureName);
        var mat = new Material(Shader.Find("Standard"));
        mat.name = "MAT_" + textureName;
        mat.SetFloat("_Glossiness", 0.2f);

        if (texture != null)
        {
            mat.mainTexture = texture;
        }
        else
        {
            mat.color = new Color(0.6f, 0.6f, 0.7f);
            Debug.LogWarning("Missing texture in Resources/Textures: " + textureName);
        }

        materials[textureName] = mat;
        return mat;
    }

    private Material CreateSunMaterial()
    {
        var mat = new Material(Shader.Find("Standard"));
        mat.name = "MAT_Sun";
        mat.color = new Color(1.0f, 0.72f, 0.2f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(1.2f, 0.7f, 0.18f));
        mat.SetFloat("_Glossiness", 0.0f);
        return mat;
    }

    private Material CreateFallbackMaterial(string materialName, Color color)
    {
        if (materials.TryGetValue(materialName, out var existing))
        {
            return existing;
        }

        var mat = new Material(Shader.Find("Standard"));
        mat.name = materialName;
        mat.color = color;
        mat.SetFloat("_Glossiness", 0.3f);
        materials[materialName] = mat;
        return mat;
    }

    private void HandleClick()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 200.0f))
        {
            return;
        }

        var body = hit.collider.GetComponent<CelestialBody>();
        if (body == null)
        {
            return;
        }

        SelectBody(body, playSound: true);
    }

    private void HandleScrollZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.001f)
        {
            return;
        }

        if (focusTarget == null)
        {
            mainViewDistance = Mathf.Clamp(mainViewDistance - (scroll * ScrollZoomSpeed), MinMainViewDistance, MaxMainViewDistance);
            defaultCameraPosition = new Vector3(0.0f, 5.0f, -mainViewDistance);
            return;
        }

        float minDynamicFocusDistance = Mathf.Max(MinFocusDistance, focusTarget.localScale.x * 1.2f);
        currentFocusDistance = Mathf.Clamp(currentFocusDistance - (scroll * ScrollZoomSpeed * 0.35f), minDynamicFocusDistance, MaxFocusDistance);
    }

    private void SelectBody(CelestialBody body, bool playSound)
    {
        focusTarget = body.transform;
        focusDistance = body.FocusDistance;
        currentFocusDistance = focusDistance;
        focusHeight = body.FocusHeight;
        focusFact = body.FactText;
        factText.text = body.DisplayName + ": " + focusFact;

        body.PlayPulseEffect();

        if (playSound && audioSource != null && audioSource.clip != null)
        {
            audioSource.pitch = 1.0f + Random.Range(-0.1f, 0.1f);
            audioSource.Play();
        }
    }

    private void ReturnToMainView()
    {
        focusTarget = null;
        factText.text = "Click a planet or the moon to learn a short fact.";
    }

    private void UpdateCameraTransition(float deltaTime)
    {
        Vector3 desiredPos;
        Quaternion desiredRot;

        if (focusTarget == null)
        {
            desiredPos = defaultCameraPosition;
            desiredRot = Quaternion.LookRotation((Vector3.zero - desiredPos).normalized, Vector3.up);
        }
        else
        {
            var direction = (focusTarget.position - Vector3.zero).normalized;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = Vector3.back;
            }

            desiredPos = focusTarget.position - (direction * currentFocusDistance) + (Vector3.up * focusHeight);
            desiredRot = Quaternion.LookRotation((focusTarget.position - desiredPos).normalized, Vector3.up);
        }

        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPos, deltaTime * CameraMoveSpeed);
        mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, desiredRot, deltaTime * CameraRotateSpeed);
    }

    private AudioClip CreateClickTone()
    {
        const int sampleRate = 44100;
        const float duration = 0.11f;
        const float frequency = 820f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        var clip = AudioClip.Create("ClickTone", sampleCount, 1, sampleRate, false);
        var data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Clamp01(1f - (i / (float)sampleCount));
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.2f;
        }

        clip.SetData(data, 0);
        return clip;
    }

    private static Font GetBuiltinFont()
    {
        var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (legacy != null)
        {
            return legacy;
        }

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}

public sealed class CelestialBody : MonoBehaviour
{
    public string DisplayName { get; private set; }
    public string FactText { get; private set; }
    public float FocusDistance { get; private set; }
    public float FocusHeight { get; private set; }

    private Vector3 originalScale;
    private float pulseTimer;

    public void Initialize(string displayName, string factText, float focusDistance, float focusHeight)
    {
        DisplayName = displayName;
        FactText = factText;
        FocusDistance = focusDistance;
        FocusHeight = focusHeight;
        originalScale = transform.localScale;
    }

    private void Update()
    {
        if (pulseTimer <= 0f)
        {
            return;
        }

        pulseTimer -= Time.deltaTime;
        float normalized = Mathf.Clamp01(pulseTimer / 0.25f);
        float scaleFactor = 1f + (0.18f * Mathf.Sin((1f - normalized) * Mathf.PI));
        transform.localScale = originalScale * scaleFactor;

        if (pulseTimer <= 0f)
        {
            transform.localScale = originalScale;
        }
    }

    public void PlayPulseEffect()
    {
        pulseTimer = 0.25f;
    }
}

public struct OrbitMotion
{
    private readonly Transform pivot;
    private readonly float degreesPerSecond;

    public OrbitMotion(Transform pivot, float degreesPerSecond)
    {
        this.pivot = pivot;
        this.degreesPerSecond = degreesPerSecond;
    }

    public void Tick(float deltaTime)
    {
        if (pivot == null)
        {
            return;
        }

        pivot.Rotate(Vector3.up, degreesPerSecond * deltaTime, Space.Self);
    }
}

public struct SelfRotation
{
    private readonly Transform target;
    private readonly float degreesPerSecond;

    public SelfRotation(Transform target, float degreesPerSecond)
    {
        this.target = target;
        this.degreesPerSecond = degreesPerSecond;
    }

    public void Tick(float deltaTime)
    {
        if (target == null)
        {
            return;
        }

        target.Rotate(Vector3.up, degreesPerSecond * deltaTime, Space.Self);
    }
}
