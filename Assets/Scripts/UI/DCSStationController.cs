using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// OLIVIA VR — DCSStationController.cs
///
/// Pusat kontrol terpadu untuk SEMUA stasiun parameter di meja DCS.
/// Satu komponen ini meng-handle 7 stasiun fisik di console (Flow, AcidRatio,
/// AcidStroke, pH, Suhu, Tekanan, RPM). Tiap stasiun punya:
///   - readout digital 3D (TextMeshPro) di housing display
///   - tombol fisik [+] dan [-] (XRSimpleInteractable) di meja
///
/// Mekanisme semua level "digabung jadi 1" di console ini:
///   - Stasiun yang relevan dengan level aktif akan HIGHLIGHT (readout terang +
///     label aktif), stasiun lain redup/locked.
///   - Menekan [+]/[-] mengubah nilai dan langsung dikirim ke GameLevelManager
///     (SetFlowRate / SetAcidRatio / SetSuhu / SetTekanan / SetRPM / SetPH),
///     sehingga monitor DCS besar ikut update.
///
/// Anchor di FBX (DCS_Panel) dipakai untuk auto-bind:
///   A_PARAM_{Key}_DISP, A_PARAM_{Key}_PLUS, A_PARAM_{Key}_MINUS
/// Tombol fisik dicari di "Button DCS/Btn_{Key}_Plus" & "Btn_{Key}_Minus"
/// (Flow & AcidRatio pakai nama lama: Btn_FlowPlus/Minus, Btn_AcidPlus/Minus).
/// </summary>
public class DCSStationController : MonoBehaviour
{
    public enum ParamType { Flow, AcidRatio, AcidStroke, PH, Suhu, Tekanan, RPM }

    [System.Serializable]
    public class Station
    {
        public ParamType type;
        public string key;            // anchor key (Flow, AcidRatio, ...)
        public string label;          // tampil di readout
        public string unit;
        public float min, max, step, target, tol, start;
        public GameLevelManager.GameLevel activeLevel; // level di mana stasiun ini "hidup"
        public bool ownButtons;       // true = controller ini yang wire tombol +/-.
                                      // false = sudah di-wire controller lain (Flow/Acid).

        [HideInInspector] public float value;
        [HideInInspector] public TextMeshPro readout;
        [HideInInspector] public XRSimpleInteractable btnPlus;
        [HideInInspector] public XRSimpleInteractable btnMinus;
        [HideInInspector] public bool active;
    }

    [Header("Auto-bind roots")]
    [SerializeField] private string _panelRootName = "DCS_Panel_NEW";
    [SerializeField] private string _buttonRootName = "Button DCS";

    [Header("Warna readout")]
    [SerializeField] private Color _warnaAktif = new Color(0.25f, 1f, 0.45f);
    [SerializeField] private Color _warnaTarget = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color _warnaJauh = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color _warnaRedup = new Color(0.35f, 0.4f, 0.45f);

    private readonly List<Station> _stations = new List<Station>();
    private Transform _panelRoot;
    private Transform _buttonRoot;
    private TMP_FontAsset _font;

    private void Start()
    {
        _panelRoot = GameObject.Find(_panelRootName)?.transform;
        _buttonRoot = GameObject.Find(_buttonRootName)?.transform;
        _font = TMP_Settings.defaultFontAsset;

        BuildStationDefs();
        BindStations();

        GameLevelManager.OnLevelStarted += OnLevelStarted;
        if (GameLevelManager.Instance != null)
            OnLevelStarted(GameLevelManager.Instance.CurrentLevel);
        else
            RefreshAll();
    }

    private void OnDestroy()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
    }

    private void Update()
    {
        // Readout selalu sinkron dengan nilai GLM (truth), apa pun yang mengubahnya
        // (tombol desk ini, panel acid Level6, atau controller lain).
        var glm = GameLevelManager.Instance;
        if (glm == null) return;
        foreach (var s in _stations)
        {
            float v = s.value;
            switch (s.type)
            {
                case ParamType.Flow: v = glm.FlowRate; break;
                case ParamType.AcidRatio: v = glm.AcidRatio; break;
                case ParamType.AcidStroke: v = glm.AcidStroke; break;
                case ParamType.PH: v = glm.PH; break;
                case ParamType.Suhu: v = glm.Suhu; break;
                case ParamType.Tekanan: v = glm.Tekanan; break;
                case ParamType.RPM: v = glm.RPM; break;
            }
            if (!Mathf.Approximately(v, s.value))
            {
                s.value = v;
                RefreshStation(s);
            }
        }
    }

    private void BuildStationDefs()
    {
        _stations.Clear();
        _stations.Add(new Station{ type=ParamType.Flow, key="Flow", label="FLOW RATE", unit="m\u00b3/h",
            min=0, max=600, step=50, target=450, tol=10, start=0, ownButtons=true,
            activeLevel=GameLevelManager.GameLevel.Level4_SlurryPump });
        _stations.Add(new Station{ type=ParamType.AcidRatio, key="AcidRatio", label="ACID DOSE", unit="kg/t",
            min=0, max=500, step=10, target=350, tol=10, start=0, ownButtons=true,
            activeLevel=GameLevelManager.GameLevel.Level6_AcidInjection });
        _stations.Add(new Station{ type=ParamType.AcidStroke, key="AcidStroke", label="PUMP STROKE", unit="%",
            min=0, max=100, step=5, target=70, tol=5, start=0, ownButtons=true,
            activeLevel=GameLevelManager.GameLevel.Level6_AcidInjection });
        _stations.Add(new Station{ type=ParamType.PH, key="pH", label="pH LEACH", unit="pH",
            min=0, max=14, step=0.1f, target=1.0f, tol=0.2f, start=7f, ownButtons=true,
            activeLevel=GameLevelManager.GameLevel.Level6_AcidInjection });
        _stations.Add(new Station{ type=ParamType.Suhu, key="Suhu", label="TEMP", unit="\u00b0C",
            min=0, max=300, step=1, target=252, tol=3, start=25f, ownButtons=true,
            activeLevel=GameLevelManager.GameLevel.Level8_Monitoring });
        _stations.Add(new Station{ type=ParamType.Tekanan, key="Tekanan", label="PRESSURE", unit="atm",
            min=0, max=80, step=0.5f, target=47.5f, tol=2.5f, start=1f, ownButtons=true,
            activeLevel=GameLevelManager.GameLevel.Level8_Monitoring });
        _stations.Add(new Station{ type=ParamType.RPM, key="RPM", label="AGITATOR", unit="RPM",
            min=0, max=120, step=5, target=60, tol=3, start=0, ownButtons=true,
            activeLevel=GameLevelManager.GameLevel.Level8_Monitoring });
    }

    private void BindStations()
    {
        foreach (var s in _stations)
        {
            s.value = s.start;

            // Reuse readouts authored in the scene so their transform and typography
            // can be adjusted directly in Edit Mode.
            Transform disp = FindAnchor($"A_PARAM_{s.key}_DISP");
            if (disp != null)
            {
                Transform existing = disp.Find($"Readout_{s.key}");
                TextMeshPro tmp = existing != null ? existing.GetComponent<TextMeshPro>() : null;

                if (tmp == null)
                {
                    var go = new GameObject($"Readout_{s.key}");
                    go.transform.SetParent(disp, true);
                    go.transform.position = disp.position + Vector3.up * 0.004f;
                    go.transform.rotation = Quaternion.Euler(90f, 180f, 0f);

                    float ls = disp.lossyScale.x;
                    if (ls < 0.0001f) ls = 1f;
                    go.transform.localScale = Vector3.one * (1f / ls);
                    tmp = go.AddComponent<TextMeshPro>();
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.enableWordWrapping = true;
                    tmp.rectTransform.sizeDelta = new Vector2(0.42f, 0.15f);
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 0.05f;
                    tmp.fontSizeMax = 1.0f;
                }

                if (_font != null && tmp.font == null) tmp.font = _font;
                s.readout = tmp;
            }

            // buttons — hanya wire kalau controller ini yang punya
            if (s.ownButtons)
            {
                s.btnPlus = FindButton(PlusName(s.key));
                s.btnMinus = FindButton(MinusName(s.key));
                var capture = s;
                if (s.btnPlus != null)
                    s.btnPlus.selectEntered.AddListener(_ => Nudge(capture, +1));
                if (s.btnMinus != null)
                    s.btnMinus.selectEntered.AddListener(_ => Nudge(capture, -1));
            }
        }
    }

    private string PlusName(string key)
    {
        if (key == "Flow") return "Btn_FlowPlus";
        if (key == "AcidRatio") return "Btn_AcidPlus";
        return $"Btn_{key}_Plus";
    }
    private string MinusName(string key)
    {
        if (key == "Flow") return "Btn_FlowMinus";
        if (key == "AcidRatio") return "Btn_AcidMinus";
        return $"Btn_{key}_Minus";
    }

    private Transform FindAnchor(string n)
    {
        if (_panelRoot == null) return null;
        foreach (var t in _panelRoot.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    private XRSimpleInteractable FindButton(string n)
    {
        if (_buttonRoot == null) return null;
        var t = _buttonRoot.Find(n);
        return t != null ? t.GetComponent<XRSimpleInteractable>() : null;
    }

    private void Nudge(Station s, int dir)
    {
        if (!s.active) return;   // locked outside its level
        s.value = Mathf.Clamp(s.value + dir * s.step, s.min, s.max);
        PushToGLM(s);
        RefreshStation(s);
    }

    private void PushToGLM(Station s)
    {
        var glm = GameLevelManager.Instance;
        if (glm == null) return;
        switch (s.type)
        {
            case ParamType.Flow: glm.SetFlowRate(s.value); break;
            case ParamType.AcidRatio: glm.SetAcidRatio(s.value); break;
            case ParamType.PH: glm.SetPH(s.value); break;
            case ParamType.Suhu: glm.SetSuhu(s.value); break;
            case ParamType.Tekanan: glm.SetTekanan(s.value); break;
            case ParamType.RPM: glm.SetRPM(s.value); break;
            case ParamType.AcidStroke: glm.SetAcidStroke(s.value); break;
        }
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        foreach (var s in _stations)
        {
            s.active = (s.activeLevel == level);
            if (s.ownButtons)
            {
                if (s.btnPlus != null) s.btnPlus.enabled = s.active;
                if (s.btnMinus != null) s.btnMinus.enabled = s.active;
            }
        }
        RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (var s in _stations) RefreshStation(s);
    }

    private void RefreshStation(Station s)
    {
        if (s.readout == null) return;
        bool onTarget = Mathf.Abs(s.value - s.target) <= s.tol;
        string status = !s.active ? "STANDBY" : (onTarget ? "OK SOP" : (s.value < s.target ? "NAIK" : "TURUN"));
        string val = (s.step < 1f) ? s.value.ToString("F1") : s.value.ToString("F0");
        string tgt = (s.step < 1f) ? s.target.ToString("F1") : s.target.ToString("F0");
        s.readout.text = $"{s.label}\n<size=160%><b>{val}</b></size> {s.unit}\nTGT {tgt} · {status}";
        if (!s.active) s.readout.color = _warnaRedup;
        else if (onTarget) s.readout.color = _warnaTarget;
        else s.readout.color = _warnaJauh;
    }
}
