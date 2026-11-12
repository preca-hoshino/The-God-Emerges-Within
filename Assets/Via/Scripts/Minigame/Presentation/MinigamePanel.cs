using UnityEngine;
using Via.Minigame.Runtime;

namespace Via.Minigame.Presentation
{
    public sealed class MinigamePanel : MonoBehaviour
    {
        [SerializeField] private MinigameController controller;
        [SerializeField] private CanvasPointerInput pointerInput;
        [SerializeField] private NeuronToolController tools;
        [SerializeField] private Font uiFont;
        private bool resultPanel;
        private string message;
        private string sessionMessage = "点击虚线范围内放置吸引点，形成直线或折线。";

        private void OnEnable() { message = sessionMessage; resultPanel = false; }
        private void OnDisable()
        {
            resultPanel = false;
            if (pointerInput != null) pointerInput.IsBlocked = false;
        }

        private void OnGUI()
        {
            if (controller == null || !controller.IsOpen) return;
            Font previousFont = GUI.skin.font;
            bool previousEnabled = GUI.enabled;
            try
            {
                if (uiFont != null) GUI.skin.font = uiFont;
                DrawPanel();
            }
            finally
            {
                GUI.skin.font = previousFont;
                GUI.enabled = previousEnabled;
            }
        }

        private void DrawPanel()
        {
            GUI.Label(new Rect(24, 12, Screen.width - 240, 24), "当前目标：直线 / 折线 | 工具：" + (tools.SelectedTool == NeuronToolController.Tool.Pull ? "牵引" : "吸引点"));
            GUI.enabled = controller.CanEdit && !resultPanel;
            if (GUI.Button(new Rect(24, 42, 110, 30), "重新开始"))
            {
                tools.CancelDrag(); controller.ResetLevel(); message = sessionMessage;
            }
            if (GUI.Button(new Rect(146, 42, 110, 30), "提交检查"))
            {
                tools.CancelDrag();
                var outcome = controller.CheckResult();
                message = outcome == null ? "未达成，或路径仍在生长。" : "已达成：" + outcome.displayName;
                if (outcome != null) { resultPanel = true; pointerInput.IsBlocked = true; controller.PauseLevel(); }
            }
            if (GUI.Button(new Rect(268, 42, 90, 30), "吸引点")) tools.SelectTool(NeuronToolController.Tool.Attractor);
            if (GUI.Button(new Rect(370, 42, 90, 30), "牵引")) tools.SelectTool(NeuronToolController.Tool.Pull);
            GUI.enabled = controller.CanUndo && !resultPanel;
            if (GUI.Button(new Rect(472, 42, 90, 30), "撤销")) { tools.CancelDrag(); controller.UndoOperation(); message = "已恢复上一步操作前的完整现场。"; }
            GUI.enabled = true;
            GUI.Label(new Rect(24, 80, Screen.width - 48, 24), tools.Feedback ?? message);
            if (!resultPanel) return;
            Rect panel = new Rect(Screen.width / 2f - 180, Screen.height / 2f - 70, 360, 140);
            GUI.Box(panel, "确认当前结果？");
            if (GUI.Button(new Rect(panel.x + 24, panel.y + 55, 140, 36), "返回修改"))
            {
                resultPanel = false; pointerInput.IsBlocked = false; controller.ResumeLevel();
            }
            if (GUI.Button(new Rect(panel.x + 196, panel.y + 55, 140, 36), "确认结果"))
            {
                resultPanel = false; pointerInput.IsBlocked = false; controller.ResumeLevel(); controller.ConfirmResult();
            }
        }

        public bool HasResultPanel => resultPanel;
    }
}
