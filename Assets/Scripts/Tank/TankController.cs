using System;
using Cinemachine;
using UnityEngine;
using Zenject;

namespace Tanks
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BaseInputController))]
    public class TankController : MonoBehaviour
    {
        private const float c_convertMeterInSecFromKmInH = 3.6f;

        private Rigidbody _body;
        private BaseInputController _controller;

        private Vector3 _prevPosition;

        [Inject]
        private CinemachineVirtualCamera _camera;

        [Header("---References---"), SerializeField]
        [Tooltip("Ссылки на четыре колеса танка")]
        private Wheel[] _wheels = new Wheel[4];
        [SerializeField, Tooltip("Источник звука скольжения шин по поверхности")]
        private AudioSource _skidAudioSource;
        [SerializeField, Tooltip("Графики мощности двигателя на разных передачах\ntime - Speed | value - Torque")]
        private TransmissionSettings _transmission;

        [SerializeField, Space, Range(5f, 50f)]
        private float _maxSteerAngle = 25f;

        [SerializeField, Min(0f)]
        private float _maxHandbrakeTorque = float.MaxValue;
        [SerializeField]
        private Vector3 _centreOfMass;

        [SerializeField, Tooltip("Дополнительная сила придавливания танка к земле. Улучшает сцепление с трассой")]
        private float _downforce = 100f;
        [SerializeField, Tooltip("Пороговое значение, при котором скольжение колеса создает эффекты и звуки")]
        private float _slipLimit = .3f;
        [SerializeField, Tooltip("Множитель мощности двигателя при заднем ходе")]
        private float _reverseMult = .4f;

        [SerializeField, Range(10f, 300f)]
        private float _maxSpeedFOV = 300f;
        [SerializeField]
        private Vector2 _fov = new(40f, 40f);

#if UNITY_EDITOR
        [SerializeField]
        private bool _debugTorque;
#endif

        public float CurrentSpeed { get; private set; }
        public float EngineSpeed => _transmission != null ? _transmission.EngineSpeed(CurrentSpeed) : 0f;

        private void Start()
        {
            _body = GetComponent<Rigidbody>();
            if (_body != null)
                _body.centerOfMass = _centreOfMass;

            _controller = GetComponent<BaseInputController>();
            _prevPosition = transform.position;

            if (_skidAudioSource == null)
            {
                _skidAudioSource = gameObject.AddComponent<AudioSource>();
                _skidAudioSource.playOnAwake = false;
                _skidAudioSource.loop = true;
            }

#if UNITY_EDITOR
            bool error = false;
            if (_transmission == null)
            {
                Debug.LogError("Need set Transmission!", gameObject);
                error = true;
            }
            if (_wheels == null || _wheels.Length == 0 || Array.FindIndex(_wheels, t => t == null) != -1)
            {
                Debug.LogError("Need configure Wheels!", gameObject);
                error = true;
            }
            if (error) UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void FixedUpdate()
        {
            if (_controller == null) return;

            _controller.ManualUpdate();

            if (_wheels != null && _wheels.Length >= 2)
            {
                float angle = _controller.TankRotate * _maxSteerAngle;
                _wheels[0].SteerAngle = angle;
                _wheels[1].SteerAngle = angle;
            }

            CalculateSpeed();
            ApplyDrive();
            AddDownForce();
            CheckForWheelSpin();
        }

        private void CalculateSpeed()
        {
            Vector3 position = transform.position;
            position.y = 0f;

            float distance = Vector3.Distance(_prevPosition, position);
            _prevPosition = position;

            CurrentSpeed = (float)Math.Round(distance / Time.deltaTime * c_convertMeterInSecFromKmInH, 1);

            if (_camera != null)
            {
                _camera.m_Lens.FieldOfView = Mathf.Lerp(_fov.x, _fov.y,
                    Mathf.InverseLerp(0f, _maxSpeedFOV, CurrentSpeed));
            }
        }

        private void ApplyDrive()
        {
            if (_wheels == null || _transmission == null) return;

            float torque = _controller.Acceleration * _transmission.GetTorque(CurrentSpeed);
            if (_controller.Acceleration < 0f)
                torque *= _reverseMult;

#if UNITY_EDITOR
            if (_debugTorque)
                Debug.Log($"Torque: {torque}");
#endif

            float handbreak = _controller.HandBrake ? _maxHandbrakeTorque : 0f;

            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != null)
                {
                    _wheels[i].Torque = torque;
                    _wheels[i].Brake = handbreak;
                }
            }
        }

        private void AddDownForce()
        {
            if (_body == null) return;

            Vector3 force = -transform.up * (_downforce * _body.velocity.magnitude);
            _body.AddForce(force);
        }

        private void CheckForWheelSpin()
        {
            if (_wheels == null || _skidAudioSource == null) return;

            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] == null) continue;

                WheelHit wheelHit = _wheels[i].GetGroundHit;

                bool isSlipping = Mathf.Abs(Mathf.Max(wheelHit.forwardSlip, wheelHit.sidewaysSlip)) >= _slipLimit;

                if (isSlipping)
                {
                    if (!_skidAudioSource.isPlaying)
                        _skidAudioSource.Play();
                }
                else
                {
                    if (_skidAudioSource.isPlaying)
                        _skidAudioSource.Stop();
                }
            }
        }

        private void Update()
        {
            if (_wheels == null) return;

            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != null)
                    _wheels[i].UpdateVisual();
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.TransformPoint(_centreOfMass), 0.2f);
            }
        }
    }
}