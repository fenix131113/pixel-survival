using System.Collections;
using GameAssembly.HealthSystem;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAssembly.PlayerSystem.View
{
    public class PlayerHealthLabelView : NetworkBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image fillImg;

        private AHealthObject _healthObject;

        private void Start()
        {
            if (isServerOnly)
                return;
            
            StartCoroutine(WaitForPlayer());
        }

        private void OnDestroy()
        {
            if (isServerOnly &&  _healthObject)
                return;

            Expose();
        }

        private void Draw(int _, int __)
        {
            label.text = $"{_healthObject.GetHealth()}/{_healthObject.GetMaxHealth()}";
            fillImg.fillAmount = (float)_healthObject.GetHealth() / _healthObject.GetMaxHealth();
        }

        private void Bind() => _healthObject.OnHealthChanged += Draw;

        private void Expose() => _healthObject.OnHealthChanged += Draw;

        private IEnumerator WaitForPlayer()
        {
            yield return new WaitUntil(() => NetworkClient.localPlayer);

            _healthObject = NetworkClient.localPlayer.GetComponent<AHealthObject>();

            Bind();
            Draw(0, 0);
        }
    }
}