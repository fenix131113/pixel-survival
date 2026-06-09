using UnityEngine;
using UnityEngine.EventSystems;

namespace GameAssembly.Utils.Ui
{
    public class ArrowButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject arrow;

        public void OnPointerEnter(PointerEventData eventData) => arrow.gameObject.SetActive(true);

        public void OnPointerExit(PointerEventData eventData) => arrow.gameObject.SetActive(false);
    }
}