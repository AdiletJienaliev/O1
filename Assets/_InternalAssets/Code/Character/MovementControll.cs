using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Character
{
    public class MovementControll : MonoBehaviour
    {
        [SerializeField] private Camera cameraMain;
        //[SerializeField] private NavMeshAgent agent;
        [SerializeField] private CharacterAnimatorController characterAnimatorController;
        [SerializeField] private Joystick joystick;
        
        [Header("Settings")]
        [SerializeField] private float movementSpeed;
        [SerializeField] private float rotationSpeed;

        private void OnValidate()
        {
            cameraMain = Camera.main;
            //agent = GetComponent<NavMeshAgent>();
            characterAnimatorController ??= GetComponent<CharacterAnimatorController>();
        }

        private void Awake() => OnValidate();

        [SerializeField] Vector2 moveDirection  = Vector2.zero;
        private void Update()
        {
            /*if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(cameraMain.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
                {
                    target = hit.point;
                    agent.SetDestination(target);
                }
            }*/
            
            moveDirection.x = joystick.Horizontal;
            moveDirection.y = joystick.Vertical;
            
            if (moveDirection != Vector2.zero)
            {
                Vector3 velocity = new Vector3(moveDirection.x, 0, moveDirection.y);
                Quaternion rotation = Quaternion.LookRotation(velocity);

                transform.position += (velocity * Time.deltaTime * movementSpeed);
                transform.rotation = rotation;
            }

            /*animDirection = Vector2.zero;
            Vector3 forward = transform.forward;
            float angle = Vector3.SignedAngle(forward, velocity, Vector3.up);

            if (angle >= -45f && angle <= 45f)
            {
                // Вперёд (с отклонениями)
                animDirection.x = angle / 90f; // -0.5 ... 0 ... 0.5
                animDirection.y = 1;
            }
            else if (angle > 45f && angle < 135f)
            {
                // Вправо
                animDirection.x = 1;
                animDirection.y = 0;
            }
            else if (angle < -45f && angle > -135f)
            {
                // Влево
                animDirection.x = -1;
                animDirection.y = 0;
            }
            else
            {
                // Назад (с отклонениями)
                animDirection.x = (angle > 0 ? 1 : -1) * (1 - Mathf.Abs(angle) / 180f); // ±0.5
                animDirection.y = -1;
            }

            if(velocity.magnitude < 0.05f) animDirection = Vector2.zero;*/
            
            characterAnimatorController.SetRun(moveDirection.magnitude);
        }
    }
}

