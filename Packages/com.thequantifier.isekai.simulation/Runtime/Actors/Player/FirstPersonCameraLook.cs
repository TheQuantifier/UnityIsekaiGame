using UnityEngine;
using UnityIsekaiGame.Configuration;
using UnityIsekaiGame.Input;

namespace UnityIsekaiGame.Player
{
    public sealed class FirstPersonCameraLook : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMovementSettings movementSettings;
        [SerializeField] private Transform yawRoot;
        [SerializeField] private Transform pitchRoot;
        [SerializeField] private bool lockCursorOnStart = true;

        private float pitch;

        private void Start()
        {
            PlayerCursorMode.SetMouseLookEnabled(lockCursorOnStart);
        }

        private void Update()
        {
            if (input == null || movementSettings == null || yawRoot == null || pitchRoot == null)
            {
                return;
            }

            Vector2 look = input.Look;
            float sensitivity = input.IsPointerLook ? movementSettings.MouseSensitivity : movementSettings.GamepadSensitivity * Time.deltaTime;
            float yawDelta = look.x * sensitivity;
            float pitchDelta = look.y * sensitivity;

            yawRoot.Rotate(Vector3.up, yawDelta, Space.Self);
            pitch = Mathf.Clamp(pitch - pitchDelta, movementSettings.PitchLimits.x, movementSettings.PitchLimits.y);
            pitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void SyncToCurrentRotationForPersistenceRestore()
        {
            if (pitchRoot == null)
            {
                pitch = 0f;
                return;
            }

            pitch = NormalizePitch(pitchRoot.localEulerAngles.x);
            pitchRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private static float NormalizePitch(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
