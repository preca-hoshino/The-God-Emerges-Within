using UnityEngine;
using Via.Story.Domain;

namespace Via.Application
{
    public sealed class GameScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private GameObject minigameViewRoot;
        private void Start()
        {
            if (controller == null || controller.Flow == null || minigameViewRoot == null)
            {
                Debug.LogError("Bind game controller and minigame presentation root.", this); enabled = false; return;
            }
            controller.Flow.Changed += Refresh;
            Refresh();
        }
        private void Refresh() => minigameViewRoot.SetActive(controller.Flow.Stage == FlowStage.Minigame);
        private void OnDestroy() { if (controller != null && controller.Flow != null) controller.Flow.Changed -= Refresh; }
    }
}
