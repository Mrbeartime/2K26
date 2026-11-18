using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    [DisallowMultipleComponent]
    public sealed class CameraSettingsUI : MonoBehaviour
    {
        [SerializeField] private Toggle cameraShake;
        [SerializeField] private Toggle actionZoom;

        private void OnEnable()
        {
            if (cameraShake != null) cameraShake.onValueChanged.AddListener(SetCameraShake);
            if (actionZoom != null) actionZoom.onValueChanged.AddListener(SetActionZoom);
            GameSettings.CameraChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (cameraShake != null) cameraShake.onValueChanged.RemoveListener(SetCameraShake);
            if (actionZoom != null) actionZoom.onValueChanged.RemoveListener(SetActionZoom);
            GameSettings.CameraChanged -= Refresh;
        }

        private void Refresh()
        {
            if (cameraShake != null) cameraShake.SetIsOnWithoutNotify(GameSettings.CameraShakeEnabled);
            if (actionZoom != null) actionZoom.SetIsOnWithoutNotify(GameSettings.ActionZoomEnabled);
        }

        public void SetCameraShake(bool enabled) => GameSettings.SetCameraShake(enabled);
        public void SetActionZoom(bool enabled) => GameSettings.SetActionZoom(enabled);
    }
}
