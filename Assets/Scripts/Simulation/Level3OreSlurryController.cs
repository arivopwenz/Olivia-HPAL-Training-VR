using System.Collections;
using UnityEngine;

/// <summary>
/// Mengatur sub-sequence Level 3:
/// laporan HT awal, fade ke area crusher, observasi ore + air, slurry 25%, lalu siap laporan akhir.
/// </summary>
public class Level3OreSlurryController : MonoBehaviour
{
    [Header("=== Referensi Pemain ===")]
    [SerializeField] private Transform _playerRigRoot;
    [SerializeField] private CharacterController _playerCharacterController;

    [Header("=== Titik Teleport ===")]
    [SerializeField] private Transform _teleportTargetField;
    [SerializeField] private Transform _teleportTargetObservation;
    [SerializeField] private Transform _teleportTargetDcs;

    [Header("=== Visual Ore dan Air ===")]
    [SerializeField] private Transform _oreMover;
    [SerializeField] private Transform _oreStartPoint;
    [SerializeField] private Transform _oreEndPoint;
    [SerializeField] private GameObject _waterFx;
    [SerializeField] private GameObject[] _aktifSaatObservasi;

    [Header("=== Visual Level Slurry ===")]
    [SerializeField] private Transform _slurryFill;
    [SerializeField] private Transform _slurryBatas25;
    [SerializeField] private Vector3 _slurryLocalScaleAwal = new Vector3(1f, 0.08f, 1f);
    [SerializeField] private Vector3 _slurryLocalScaleTarget25 = new Vector3(1f, 0.25f, 1f);
    [SerializeField] private Vector3 _slurryLocalPosAwal = new Vector3(0f, -0.45f, 0f);
    [SerializeField] private Vector3 _slurryLocalPosTarget25 = new Vector3(0f, -0.18f, 0f);

    [Header("=== Timing Sequence ===")]
    [SerializeField] private float _jedaSetelahLaporanAwal = 2.2f;
    [SerializeField] private float _durasiFadeKeField = 3.1f;
    [SerializeField] private float _jedaSebelumOreJalan = 0.7f;
    [SerializeField] private float _durasiGerakOre = 4.8f;
    [SerializeField] private float _jedaSetelahOreMasuk = 0.5f;
    [SerializeField] private float _durasiIsiSlurry = 5.5f;

    private PlayerHUD _hud;
    private Coroutine _sequenceCoroutine;
    private Coroutine _returnCoroutine;
    private bool _sequenceSudahDimulai;

    private void Awake()
    {
        _hud = FindObjectOfType<PlayerHUD>();
        if (_playerRigRoot == null && Camera.main != null)
            _playerRigRoot = Camera.main.transform.root;

        if (_playerCharacterController == null && _playerRigRoot != null)
            _playerCharacterController = _playerRigRoot.GetComponent<CharacterController>();

        ResetVisualState();
    }

    private void OnEnable()
    {
        GameLevelManager.OnLevelStarted += OnLevelStarted;
        GameLevelManager.OnVoiceReportAccepted += OnVoiceReportAccepted;
        GameLevelManager.OnLevelTransitionRequested += OnLevelTransitionRequested;
    }

    private void OnDisable()
    {
        GameLevelManager.OnLevelStarted -= OnLevelStarted;
        GameLevelManager.OnVoiceReportAccepted -= OnVoiceReportAccepted;
        GameLevelManager.OnLevelTransitionRequested -= OnLevelTransitionRequested;
    }

    private void OnLevelStarted(GameLevelManager.GameLevel level)
    {
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }

        _sequenceSudahDimulai = false;
        ResetVisualState();
    }

    private void OnVoiceReportAccepted(string keyword)
    {
        if (GameLevelManager.Instance == null)
            return;

        if (GameLevelManager.Instance.CurrentLevel != GameLevelManager.GameLevel.Level3_OreSlurry)
            return;

        if (GameLevelManager.Instance.CurrentLevel3Phase != GameLevelManager.Level3Phase.LaporanAwalDiterima)
            return;

        if (_sequenceSudahDimulai)
            return;

        _sequenceSudahDimulai = true;
        _sequenceCoroutine = StartCoroutine(MainkanSequenceLevel3());
    }

    private void OnLevelTransitionRequested(GameLevelManager.GameLevel fromLevel, GameLevelManager.GameLevel toLevel, float duration)
    {
        if (fromLevel != GameLevelManager.GameLevel.Level3_OreSlurry || toLevel != GameLevelManager.GameLevel.Level4_SlurryPump)
            return;

        if (_returnCoroutine != null)
            StopCoroutine(_returnCoroutine);

        _returnCoroutine = StartCoroutine(TeleportKeDcsSaatTransisi(duration));
    }

    private IEnumerator MainkanSequenceLevel3()
    {
        yield return new WaitForSeconds(_jedaSetelahLaporanAwal);

        if (_hud != null)
            _hud.PlayManualFade(_durasiFadeKeField);

        yield return new WaitForSeconds(HitungWaktuTeleport(_durasiFadeKeField));
        TeleportPlayer(_teleportTargetField);
        GameLevelManager.Instance?.NotifyLevel3FieldSequenceStarted();

        float sisaFade = Mathf.Max(0f, _durasiFadeKeField - HitungWaktuTeleport(_durasiFadeKeField));
        if (sisaFade > 0f)
            yield return new WaitForSeconds(sisaFade);

        yield return new WaitForSeconds(_jedaSebelumOreJalan);

        SetObservationObjects(true);
        if (!RefsOreLengkap())
        {
            Debug.LogWarning("[Level3OreSlurryController] Ore mover/start/end belum lengkap. Sequence Level 3 dihentikan agar quest tidak auto-centang.");
            _sequenceCoroutine = null;
            yield break;
        }

        if (_slurryFill == null)
        {
            Debug.LogWarning("[Level3OreSlurryController] SlurryFill belum di-assign. Sequence Level 3 dihentikan agar quest tidak auto-centang.");
            _sequenceCoroutine = null;
            yield break;
        }

        yield return StartCoroutine(AnimasikanOreMasukKeTank());
        GameLevelManager.Instance?.NotifyLevel3OreReachedSlurry();

        if (_teleportTargetObservation != null)
            TeleportPlayer(_teleportTargetObservation);

        if (_jedaSetelahOreMasuk > 0f)
            yield return new WaitForSeconds(_jedaSetelahOreMasuk);

        yield return StartCoroutine(AnimasikanIsiSlurrySampaiBatas());

        if (SlurrySudahMencapaiBatas25())
            GameLevelManager.Instance?.NotifyLevel3SlurryReady(25f);
        else
            Debug.LogWarning("[Level3OreSlurryController] Slurry belum mencapai batas 25%, quest belum akan dicentang.");

        _sequenceCoroutine = null;
    }

    private IEnumerator TeleportKeDcsSaatTransisi(float duration)
    {
        yield return new WaitForSeconds(HitungWaktuTeleport(duration));
        TeleportPlayer(_teleportTargetDcs);
        _returnCoroutine = null;
    }

    private IEnumerator AnimasikanOreMasukKeTank()
    {
        float elapsed = 0f;
        while (elapsed < _durasiGerakOre)
        {
            elapsed += Time.deltaTime;
            float oreT = _durasiGerakOre <= 0f ? 1f : Mathf.Clamp01(elapsed / _durasiGerakOre);
            _oreMover.position = Vector3.Lerp(_oreStartPoint.position, _oreEndPoint.position, oreT);
            yield return null;
        }

        _oreMover.position = _oreEndPoint.position;
    }

    private IEnumerator AnimasikanIsiSlurrySampaiBatas()
    {
        float elapsed = 0f;
        while (elapsed < _durasiIsiSlurry)
        {
            elapsed += Time.deltaTime;
            float slurryT = _durasiIsiSlurry <= 0f ? 1f : Mathf.Clamp01(elapsed / _durasiIsiSlurry);
            _slurryFill.localScale = Vector3.Lerp(_slurryLocalScaleAwal, _slurryLocalScaleTarget25, slurryT);
            _slurryFill.localPosition = Vector3.Lerp(_slurryLocalPosAwal, _slurryLocalPosTarget25, slurryT);
            yield return null;
        }

        _slurryFill.localScale = _slurryLocalScaleTarget25;
        _slurryFill.localPosition = _slurryLocalPosTarget25;
    }

    private void TeleportPlayer(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[Level3OreSlurryController] Target teleport belum di-assign.");
            return;
        }

        if (_playerRigRoot == null && Camera.main != null)
            _playerRigRoot = Camera.main.transform.root;

        if (_playerRigRoot == null)
        {
            Debug.LogWarning("[Level3OreSlurryController] Player rig root tidak ditemukan.");
            return;
        }

        bool restoreController = _playerCharacterController != null && _playerCharacterController.enabled;
        if (restoreController)
            _playerCharacterController.enabled = false;

        _playerRigRoot.SetPositionAndRotation(target.position, target.rotation);

        if (restoreController)
            _playerCharacterController.enabled = true;
    }

    private void ResetVisualState()
    {
        if (_oreMover != null && _oreStartPoint != null)
            _oreMover.position = _oreStartPoint.position;

        if (_slurryFill != null)
        {
            _slurryFill.localScale = _slurryLocalScaleAwal;
            _slurryFill.localPosition = _slurryLocalPosAwal;
        }

        SetObservationObjects(false);
    }

    private bool RefsOreLengkap()
    {
        return _oreMover != null && _oreStartPoint != null && _oreEndPoint != null;
    }

    private bool SlurrySudahMencapaiBatas25()
    {
        if (_slurryFill == null)
            return false;

        if (_slurryBatas25 == null)
            return Vector3.Distance(_slurryFill.localPosition, _slurryLocalPosTarget25) <= 0.02f;

        return _slurryFill.position.y >= _slurryBatas25.position.y;
    }

    private void SetObservationObjects(bool active)
    {
        if (_oreMover != null)
            _oreMover.gameObject.SetActive(active);

        if (_waterFx != null)
            _waterFx.SetActive(active);

        if (_aktifSaatObservasi == null)
            return;

        for (int i = 0; i < _aktifSaatObservasi.Length; i++)
        {
            if (_aktifSaatObservasi[i] != null)
                _aktifSaatObservasi[i].SetActive(active);
        }
    }

    private float HitungWaktuTeleport(float totalDuration)
    {
        float fadeIn = Mathf.Clamp(totalDuration * 0.35f, 0.8f, 1.6f);
        float hold = Mathf.Max(0.15f, totalDuration - fadeIn - fadeIn);
        return fadeIn + (hold * 0.5f);
    }
}
