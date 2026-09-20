using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TicketToRide.Presentation
{
    public class TrainCardDeckView : MonoBehaviour, IPointerClickHandler
    {
        public event Action OnDeckClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            OnDeckClicked?.Invoke();
        }
    }
}
