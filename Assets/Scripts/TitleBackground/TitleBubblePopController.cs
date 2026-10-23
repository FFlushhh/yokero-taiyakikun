using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// タイトル画面の泡をクリックして割る。
/// 1クリックにつき、最も近い生存Particleを1個だけ削除する。
/// </summary>
public sealed class TitleBubblePopController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private ParticleSystem[] _bubbleSystems;

    [Header("Click Settings")]
    [SerializeField, Range(0.1f, 2f)]
    private float _hitRadiusMultiplier = 0.9f;

    [SerializeField, Min(0f)]
    private float _minimumHitRadiusPixels = 12f;

    [Header("Pop Sounds")]
    [SerializeField] private AudioSource _popAudioSource;
    [SerializeField] private AudioClip[] _popSoundClips;

    [SerializeField, Range(0f, 1f)]
    private float _popVolume = 0.65f;

    [Header("Optional Effect")]
    [SerializeField] private ParticleSystem _popEffect;

    [SerializeField, Min(0)]
    private int _effectParticleCount = 5;

    // クリックされた泡のワールド座標を通知する。
    public event Action<Vector3> BubblePopped;

    private ParticleSystem.Particle[][] _buffers;

    private PointerEventData _pointerEventData;
    private readonly List<RaycastResult> _uiHits =
        new List<RaycastResult>(8);

    private void Awake()
    {
        if (_targetCamera == null)
        {
            _targetCamera = Camera.main;
        }

        if (_targetCamera == null ||
            _bubbleSystems == null ||
            _bubbleSystems.Length == 0)
        {
            Debug.LogError(
                "TitleBubblePopController: カメラまたは泡が未設定です。",
                this);
            enabled = false;
            return;
        }

        // 最大Particle数に合わせて配列を一度だけ確保。
        _buffers = new ParticleSystem.Particle[
            _bubbleSystems.Length][];

        for (int i = 0; i < _bubbleSystems.Length; i++)
        {
            ParticleSystem system = _bubbleSystems[i];

            if (system == null)
            {
                continue;
            }

            int capacity = Mathf.Max(1, system.main.maxParticles);
            _buffers[i] = new ParticleSystem.Particle[capacity];
        }

        if (_popEffect != null)
        {
            // Inspector側ではEmission Rateを0にする。
            // 手動Emitした粒子を更新できる状態にする。
            _popEffect.Play();
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null ||
            !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 screenPoint = mouse.position.ReadValue();

        // uGUIのボタンやロゴに対するクリックを優先する。
        if (IsPointerOverUi(screenPoint))
        {
            return;
        }

        TryPopBubble(screenPoint);
    }

    private bool IsPointerOverUi(Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            return false;
        }

        if (_pointerEventData == null)
        {
            _pointerEventData = new PointerEventData(eventSystem);
        }

        _pointerEventData.Reset();
        _pointerEventData.position = screenPoint;

        _uiHits.Clear();
        eventSystem.RaycastAll(_pointerEventData, _uiHits);

        // GraphicRaycasterによるUIヒットを優先する。
        for (int i = 0; i < _uiHits.Count; i++)
        {
            if (_uiHits[i].module is GraphicRaycaster)
            {
                return true;
            }
        }

        return false;
    }

    private void TryPopBubble(Vector2 screenPoint)
    {
        int selectedSystem = -1;
        int selectedParticle = -1;
        int selectedAliveCount = 0;

        float bestScore = float.PositiveInfinity;
        Vector3 selectedWorldPosition = Vector3.zero;

        for (int s = 0; s < _bubbleSystems.Length; s++)
        {
            ParticleSystem system = _bubbleSystems[s];

            if (system == null ||
                !system.gameObject.activeInHierarchy)
            {
                continue;
            }

            ParticleSystem.Particle[] buffer = _buffers[s];

            if (buffer == null)
            {
                continue;
            }

            int aliveCount = system.GetParticles(buffer);
            ParticleSystem.MainModule main = system.main;

            for (int i = 0; i < aliveCount; i++)
            {
                ParticleSystem.Particle particle = buffer[i];

                Vector3 worldPosition =
                    GetWorldPosition(system, main, particle.position);

                Vector3 screenPosition =
                    _targetCamera.WorldToScreenPoint(worldPosition);

                // カメラの背後は判定しない。
                if (screenPosition.z <= 0f)
                {
                    continue;
                }

                // Particleの現在サイズから半径を求める。
                float sizeScale = GetSizeScale(system, main);

                float worldRadius =
                    particle.GetCurrentSize(system) *
                    sizeScale * 0.5f;

                Vector3 edgePosition =
                    worldPosition +
                    _targetCamera.transform.right * worldRadius;

                Vector3 screenEdge =
                    _targetCamera.WorldToScreenPoint(edgePosition);

                float radiusPixels = Mathf.Max(
                    _minimumHitRadiusPixels,
                    Vector2.Distance(
                        screenPosition,
                        screenEdge) * _hitRadiusMultiplier);

                Vector2 delta =
                    screenPoint - (Vector2)screenPosition;

                float distanceSquared = delta.sqrMagnitude;
                float radiusSquared = radiusPixels * radiusPixels;

                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                // 半径で正規化して重なりを比較する。
                float score = distanceSquared / radiusSquared;

                if (score < bestScore)
                {
                    bestScore = score;
                    selectedSystem = s;
                    selectedParticle = i;
                    selectedAliveCount = aliveCount;
                    selectedWorldPosition = worldPosition;
                }
            }
        }

        if (selectedSystem < 0)
        {
            return;
        }

        // 選択されたParticleのみを削除。
        ParticleSystem targetSystem =
            _bubbleSystems[selectedSystem];

        ParticleSystem.Particle[] targetBuffer =
            _buffers[selectedSystem];

        targetBuffer[selectedParticle].remainingLifetime = -1f;

        targetSystem.SetParticles(
            targetBuffer, selectedAliveCount);

        PlayRandomPopSound();
        EmitPopEffect(selectedWorldPosition);

        BubblePopped?.Invoke(selectedWorldPosition);
    }

    private static Vector3 GetWorldPosition(
        ParticleSystem system,
        ParticleSystem.MainModule main,
        Vector3 particlePosition)
    {
        switch (main.simulationSpace)
        {
            case ParticleSystemSimulationSpace.World:
                return particlePosition;

            case ParticleSystemSimulationSpace.Custom:
                Transform custom = main.customSimulationSpace;
                return custom != null
                    ? custom.TransformPoint(particlePosition)
                    : system.transform.TransformPoint(particlePosition);

            default:
                return system.transform.TransformPoint(particlePosition);
        }
    }

    private static float GetSizeScale(
        ParticleSystem system,
        ParticleSystem.MainModule main)
    {
        Vector3 scale;

        switch (main.scalingMode)
        {
            case ParticleSystemScalingMode.Hierarchy:
                scale = system.transform.lossyScale;
                break;

            case ParticleSystemScalingMode.Local:
                scale = system.transform.localScale;
                break;

            default:
                // ShapeモードではParticleサイズにScaleを適用しない。
                return 1f;
        }

        return Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y));
    }

    private void PlayRandomPopSound()
    {
        if (_popAudioSource == null ||
            _popSoundClips == null ||
            _popSoundClips.Length == 0)
        {
            return;
        }

        // 最初の候補をランダムに選び、
        // nullならほかの候補を巡回する。
        int startIndex = UnityEngine.Random.Range(
            0, _popSoundClips.Length);

        for (int i = 0; i < _popSoundClips.Length; i++)
        {
            int index = (startIndex + i) %
                _popSoundClips.Length;

            AudioClip clip = _popSoundClips[index];

            if (clip != null)
            {
                _popAudioSource.PlayOneShot(clip, _popVolume);
                return;
            }
        }
    }

    private void EmitPopEffect(Vector3 worldPosition)
    {
        if (_popEffect == null ||
            _effectParticleCount <= 0)
        {
            return;
        }

        // Pop Effect側のSimulation SpaceはWorldにする。
        ParticleSystem.EmitParams parameters =
            new ParticleSystem.EmitParams
            {
                position = worldPosition
            };

        _popEffect.Emit(parameters, _effectParticleCount);
    }
}
