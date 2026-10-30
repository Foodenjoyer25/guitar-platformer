using UnityEngine;
using UnityEngine.EventSystems;

namespace GuitarPlatformer
{
    public enum MenuScreen
    {
        Main,
        Challenge,
        Difficulty,
        Pause,
        Reveal,
        Interval
    }

    /// <summary>
    /// Mouse bridge for the custom menu rows. Keyboard and gamepad navigation is
    /// handled by LevelManager; this only forwards pointer hover and click events.
    /// </summary>
    public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public MenuScreen screen;
        public int index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.HoverMenu(screen, index);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.ActivateMenu(screen, index);
        }
    }
}
