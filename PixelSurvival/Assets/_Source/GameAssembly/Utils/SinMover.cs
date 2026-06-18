using System;
using UnityEngine;

namespace GameAssembly.Utils
{
    public class SinMover : MonoBehaviour
    {
        [SerializeField] private Transform moveObject;
        
        [Header("Floating")]
        [SerializeField] private float floatAmplitude = 5f;
        [SerializeField] private float floatSpeed = 2f;

        private Vector3 _startPosition;

        private void Start()
        {
            _startPosition =  moveObject.localPosition;
        }

        private void Update()
        {
            var offsetY = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;

            moveObject.localPosition = _startPosition + Vector3.up * offsetY;
        }
    }
}