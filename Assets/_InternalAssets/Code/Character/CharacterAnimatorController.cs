using UnityEngine;


namespace Character
{
    [RequireComponent(typeof(Animator))]
    public class CharacterAnimatorController : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private void OnValidate()
        {
            animator ??= GetComponent<Animator>();
        }

        private void Awake() => OnValidate();

        public void SetDirection(Vector2 dir)
        {
            animator.SetFloat("x", dir.x);
            animator.SetFloat("y", dir.y);
        }

        public void SetRun(float v)
        {
            animator.SetFloat("run", v);
        }
    }
}
