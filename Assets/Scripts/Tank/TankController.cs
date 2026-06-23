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
        private float _currentSteerAngle;
        
        [Inject]
        private CinemachineVirtualCamera _camera;

        [Header("---References---"), SerializeField]
        [Tooltip("Ссылки на четыре колеса танка")]
        private Wheel[] _wheels = new Wheel[4];
        
        [SerializeField, Tooltip("Источник звука скольжения шин по поверхности")]
        private AudioSource _skidAudioSource;
        
        [SerializeField, Tooltip("Графики мощности двигателя на разных передачах")]
        private TransmissionSettings _transmission;
        
        [SerializeField, Space, Range(5f, 50f)] 
        private float _maxSteerAngle = 25f;

        [SerializeField, Min(0f)]
        private float _maxHandbrakeTorque = float.MaxValue;
        
        [SerializeField] 
        private Vector3 _centreOfMass;

        [SerializeField, Tooltip("Дополнительная сила придавливания танка к земле")] 
        private float _downforce = 100f;
        
        [SerializeField, Tooltip("Пороговое значение скольжения")] 
        private float _slipLimit = .3f;

        [SerializeField, Tooltip("Множитель мощности двигателя при заднем ходе")]
        private float _reverseMult = .4f;

        [SerializeField, Range(10f, 300f)]
        private float _maxSpeedFOV = 200f;
        
        [SerializeField]
        private Vector2 _fov = new (40f, 40f);

        public float CurrentSpeed { get; private set; }

        private void Start()
        {
            _body = GetComponent<Rigidbody>();
            _body.centerOfMass = _centreOfMass;
            _controller = GetComponent<BaseInputController>();
            _prevPosition = transform.position;

            if (_skidAudioSource == null)
            {
                _skidAudioSource = gameObject.AddComponent<AudioSource>();
                _skidAudioSource.playOnAwake = false;
                _skidAudioSource.loop = true;
            }

            Debug.Log("TankController started. Wheels count: " + _wheels.Length);
        }

        private void FixedUpdate()
        {
            _controller.ManualUpdate();

            // Поворот (только передние колёса)
            var angle = _controller.TankRotate * _maxSteerAngle;
            if (_wheels.Length > 1)
            {
                _wheels[0].SteerAngle = angle;
                _wheels[1].SteerAngle = angle;
            }

            CalculateSpeed();
            ApplyDrive();        // ← здесь тестовая версия

            AddDownForce();
            CheckForWheelSpin();
        }

        private void CalculateSpeed()
        {
            var position = transform.position;
            position.y = 0f;
            var distance = Vector3.Distance(_prevPosition, position);
            _prevPosition = position;

            CurrentSpeed = (float)Math.Round(distance / Time.deltaTime * c_convertMeterInSecFromKmInH, 1);
            
            if (_camera != null)
                _camera.m_Lens.FieldOfView = Mathf.Lerp(_fov.x, _fov.y, Mathf.InverseLerp(0f, _maxSpeedFOV, CurrentSpeed));
        }

        /// <summary>
        /// УПРОЩЁННАЯ ВЕРСИЯ ДЛЯ ТЕСТА
        /// </summary>
        private void ApplyDrive()
        {
            float acceleration = _controller.Acceleration;
            float torque = acceleration * 1800f;   // большая сила для теста

            Debug.Log($"[Tank] Acc = {acceleration:F2} | Torque = {torque:F0} | Speed = {CurrentSpeed}");

            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != null)
                {
                    _wheels[i].Torque = torque;
                    _wheels[i].Brake = 0f;
                }
            }
        }

        private void AddDownForce()
        {
            var value = -transform.up * (_downforce * _body.linearVelocity.magnitude);
            _body.AddForce(value);
        }

        private void CheckForWheelSpin()
        {
            if (_skidAudioSource == null) return;

            bool isSlipping = false;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] == null) continue;
                var hit = _wheels[i].GetGroundHit;
                if (Mathf.Abs(Mathf.Max(hit.forwardSlip, hit.sidewaysSlip)) >= _slipLimit)
                {
                    isSlipping = true;
                    break;
                }
            }

            if (isSlipping && !_skidAudioSource.isPlaying)
                _skidAudioSource.Play();
            else if (!isSlipping && _skidAudioSource.isPlaying)
                _skidAudioSource.Stop();
        }

        private void Update()
        {
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != null)
                    _wheels[i].UpdateVisual();
            }
        }
    }
}