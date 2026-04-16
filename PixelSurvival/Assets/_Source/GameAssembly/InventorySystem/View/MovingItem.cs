using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameAssembly.InventorySystem.View
{
    public class MovingItem : MonoBehaviour
    {
        [SerializeField] private RectTransform movingRect;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TMP_Text itemCountLabel;
        [SerializeField, Min(0f)] private float followSmooth = 26f;
        [SerializeField, Min(0f)] private float maxTiltAngle = 13f;
        [SerializeField, Min(0f)] private float tiltSensitivity = 0.05f;
        [SerializeField, Min(0f)] private float tiltSmoothTime = 0.06f;
        [SerializeField, Min(0f)] private float idleTiltAmplitude = 2f;
        [SerializeField, Min(0f)] private float idleBobAmplitude = 3f;
        [SerializeField, Min(0f)] private float idleFrequency = 2.5f;

        private bool _isMoving;
        private PlayerLocalInventoryManager _playerLocalInventoryManager;
        private Vector2 _smoothedMousePosition;
        private Vector2 _lastMousePosition;
        private float _currentTilt;
        private float _tiltVelocity;
        private float _idleSeed;
        private bool _hasSmoothedMousePosition;
        private bool _hasLastMousePosition;

        public bool IsMoving => _isMoving;
        public NetworkIdentity CurrentInventoryIdentity { get; private set; }
        public IInventory CurrentInventory { get; private set; }
        public int CurrentCellIndex { get; private set; } = -1;

        private void Awake()
        {
            if (!movingRect)
            {
                Debug.LogError($"{nameof(MovingItem)} requires {nameof(movingRect)} reference.", this);
                enabled = false;
                return;
            }

            if (!itemIcon)
                Debug.LogWarning($"{nameof(MovingItem)} has no {nameof(itemIcon)} reference.", this);

            if (!itemCountLabel)
                Debug.LogWarning($"{nameof(MovingItem)} has no {nameof(itemCountLabel)} reference.", this);

            _idleSeed = Random.value * 100f;
            ResetVisualState();
            HideVisual();
        }

        private void Start()
        {
            if (!enabled)
                return;

            if (NetworkServer.active && !NetworkClient.active)
                return;

            StartCoroutine(WaitForPlayer());
        }

        public void Prewarm()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (!enabled || !movingRect)
                return;

            ResetVisualState();
            HideVisual();
        }

        private void OnDisable() => ForceClose();

        private void Update()
        {
            if (!_isMoving)
                return;

            if (Mouse.current == null)
            {
                ForceClose();
                return;
            }

            var mousePosition = GetMousePosition();
            UpdatePosition(mousePosition);
            UpdateRotation(mousePosition);

            if (Mouse.current.leftButton.wasReleasedThisFrame)
                ForceClose();
        }

        public void StartDrag(NetworkIdentity inventoryIdentity, int cellIndex)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (!enabled || !movingRect || Mouse.current == null)
                return;

            if (Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed)
                return;

            if (!inventoryIdentity.TryGetComponent(out IInventory inventory))
                return;

            CurrentInventory = inventory;

            if (CurrentInventory.GetItemByIndex(cellIndex) == null)
                return;

            _isMoving = true;
            CurrentInventoryIdentity = inventoryIdentity;
            CurrentCellIndex = cellIndex;

            Draw();
            InitializeMotionState(GetMousePosition());
            ShowVisual();
        }

        public void TriggerDrop(NetworkIdentity inventoryIdentity, int cellIndex)
        {
            if (!_isMoving)
                return;

            var sourceInventoryIdentity = CurrentInventoryIdentity;
            var sourceCellIndex = CurrentCellIndex;

            ForceClose();

            if (!sourceInventoryIdentity || !_playerLocalInventoryManager)
                return;

            if (inventoryIdentity == sourceInventoryIdentity && cellIndex == sourceCellIndex)
                return;

            _playerLocalInventoryManager.Cmd_CombineCells(sourceInventoryIdentity, sourceCellIndex, inventoryIdentity,
                cellIndex, false);
        }

        public void ForceClose()
        {
            _isMoving = false;
            CurrentInventoryIdentity = null;
            CurrentInventory = null;
            CurrentCellIndex = -1;
            ResetVisualState();
            HideVisual();
        }

        private void Draw()
        {
            if (CurrentInventory == null || CurrentCellIndex < 0)
            {
                SetEmptyVisual();
                return;
            }

            var item = CurrentInventory.GetItemByIndex(CurrentCellIndex);

            if (item == null || item.Definition == null)
            {
                SetEmptyVisual();
                return;
            }

            if (itemIcon)
            {
                itemIcon.sprite = item.Definition.Icon;
                itemIcon.enabled = true;
            }

            if (itemCountLabel)
            {
                itemCountLabel.text = item.Count.ToString();
                itemCountLabel.gameObject.SetActive(true);
            }
        }

        private Vector2 GetMousePosition()
        {
            if (Mouse.current == null)
                return movingRect ? movingRect.position : Vector2.zero;

            return Mouse.current.position.ReadValue();
        }

        private IEnumerator WaitForPlayer()
        {
            while (isActiveAndEnabled && !NetworkClient.localPlayer)
                yield return null;

            if (!isActiveAndEnabled || !NetworkClient.localPlayer)
                yield break;

            _playerLocalInventoryManager = NetworkClient.localPlayer.GetComponent<PlayerLocalInventoryManager>();
        }

        private void InitializeMotionState(Vector2 mousePosition)
        {
            _smoothedMousePosition = mousePosition;
            _lastMousePosition = mousePosition;
            _hasSmoothedMousePosition = true;
            _hasLastMousePosition = true;
            _currentTilt = 0f;
            _tiltVelocity = 0f;

            movingRect.position = mousePosition;
            movingRect.localRotation = Quaternion.identity;
        }

        private void ResetVisualState()
        {
            _hasSmoothedMousePosition = false;
            _hasLastMousePosition = false;
            _currentTilt = 0f;
            _tiltVelocity = 0f;

            if (!movingRect)
                return;

            movingRect.localRotation = Quaternion.identity;
        }

        private void UpdatePosition(Vector2 mousePosition)
        {
            var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

            if (!_hasSmoothedMousePosition)
            {
                _smoothedMousePosition = mousePosition;
                _hasSmoothedMousePosition = true;
            }

            var followFactor = followSmooth <= 0f ? 1f : 1f - Mathf.Exp(-followSmooth * deltaTime);
            _smoothedMousePosition = Vector2.Lerp(_smoothedMousePosition, mousePosition, followFactor);

            var idleTime = Time.unscaledTime + _idleSeed;
            var idleBob = Mathf.Sin(idleTime * idleFrequency) * idleBobAmplitude;

            movingRect.position = _smoothedMousePosition + new Vector2(0f, idleBob);
        }

        private void UpdateRotation(Vector2 mousePosition)
        {
            var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

            if (!_hasLastMousePosition)
            {
                _lastMousePosition = mousePosition;
                _hasLastMousePosition = true;
            }

            var horizontalSpeed = (mousePosition.x - _lastMousePosition.x) / deltaTime;
            _lastMousePosition = mousePosition;

            var speedTilt = Mathf.Clamp(-horizontalSpeed * tiltSensitivity, -maxTiltAngle, maxTiltAngle);
            var idleTime = Time.unscaledTime + _idleSeed;
            var idleTilt = Mathf.Sin(idleTime * idleFrequency * 0.7f) * idleTiltAmplitude;
            var targetTilt = speedTilt + idleTilt;

            _currentTilt = tiltSmoothTime <= 0f
                ? targetTilt
                : Mathf.SmoothDampAngle(_currentTilt, targetTilt, ref _tiltVelocity, tiltSmoothTime, Mathf.Infinity,
                    deltaTime);

            movingRect.localRotation = Quaternion.Euler(0f, 0f, _currentTilt);
        }

        private void SetEmptyVisual()
        {
            if (itemIcon)
            {
                itemIcon.sprite = null;
                itemIcon.enabled = false;
            }

            if (itemCountLabel)
            {
                itemCountLabel.text = string.Empty;
                itemCountLabel.gameObject.SetActive(false);
            }
        }

        private void ShowVisual()
        {
            if (itemIcon)
                itemIcon.enabled = true;

            if (itemCountLabel)
                itemCountLabel.gameObject.SetActive(true);
        }

        private void HideVisual()
        {
            if (itemIcon)
                itemIcon.enabled = false;

            if (itemCountLabel)
                itemCountLabel.gameObject.SetActive(false);
        }
    }
}
