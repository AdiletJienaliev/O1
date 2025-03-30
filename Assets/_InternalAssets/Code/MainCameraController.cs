using System;
using UnityEngine;

public class MainCameraController : MonoBehaviour
{
    [SerializeField] private Transform target;
    
    [Header("Settings")]
    [SerializeField] private float smoothTime = 0.5f;

    private Vector3 initialOffset;
    
    private void Awake()
    {
        initialOffset = transform.position - target.position;
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, target.position + initialOffset, Time.deltaTime * smoothTime);
    }
}
