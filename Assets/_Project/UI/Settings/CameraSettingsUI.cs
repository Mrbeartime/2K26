using UnityEngine;
using UnityEngine.UI;

namespace ProjectD.Menus
{
    [DisallowMultipleComponent]
    public sealed class CameraSettingsUI : MonoBehaviour
    {
        [SerializeField] private Toggle cameraShake;
        [SerializeField] private Toggle actionZoom;
        [SerializeField] private Slider panSensitivity;

        private void OnEnable()
        {
            if (cameraShake != null) cameraShake.onValueChanged.AddListener(SetCameraShake);
            if (actionZoom != null) actionZoom.onValueChanged.AddListener(SetActionZoom);
            if (panSensitivity != null)
            {
                panSensitivity.minValue = GameSettings.MinPanSensitivity;
                panSensitivity.maxValue = GameSettings.MaxPanSensitivity;
                panSensitivity.wholeNumbers = false;
                panSensitivity.onValueChanged.AddListener(SetPanSensitivity);
            }
            GameSettings.CameraChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (cameraShake != null) cameraShake.onValueChanged.RemoveListener(SetCameraShake);
            if (actionZoom != null) actionZoom.onValueChanged.RemoveListener(SetActionZoom);
            if (panSensitivity != null) panSensitivity.onValueChanged.RemoveListener(SetPanSensitivity);
            GameSettings.CameraChanged -= Refresh;
            GameSettings.SaveCameraSettings();
        }

        private void Refresh()
        {
            if (cameraShake != null) cameraShake.SetIsOnWithoutNotify(GameSettings.CameraShakeEnabled);
            if (actionZoom != null) actionZoom.SetIsOnWithoutNotify(GameSettings.ActionZoomEnabled);
            if (panSensitivity != null) panSensitivity.SetValueWithoutNotify(GameSettings.PanSensitivity);
        }

        public void SetCameraShake(bool enabled) => GameSettings.SetCameraShake(enabled);
        public void SetActionZoom(bool enabled) => GameSettings.SetActionZoom(enabled);
        public void SetPanSensitivity(float value) => GameSettings.SetPanSensitivity(value);
    }
}
