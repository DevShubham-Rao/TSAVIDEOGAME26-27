using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SignalLost
{
    /// <summary>Tells a settings slider when the pointer lets go (sample pop on SFX, UI scale apply).</summary>
    public class SliderHook : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action OnRelease;
        public bool Dragging { get; private set; }

        public void OnPointerDown(PointerEventData e)
        {
            Dragging = true;
        }

        public void OnPointerUp(PointerEventData e)
        {
            Dragging = false;
            if (OnRelease != null) OnRelease();
        }
    }
}
