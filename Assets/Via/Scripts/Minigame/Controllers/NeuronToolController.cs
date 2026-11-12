using UnityEngine;
using Via.Minigame.Presentation;
using Via.Minigame.Domain;

namespace Via.Minigame.Runtime
{
    public sealed class NeuronToolController : MonoBehaviour
    {
        public enum Tool { Attractor, Pull, Cut, Branch, Reshape }
        public Tool SelectedTool { get; private set; }
        public int SelectedNode { get; private set; } = -1;
        public bool IsDragging { get; private set; }
        public Vector2 PreviewPosition { get; private set; }
        public Vector2 CutStart { get; private set; }
        private NeuronNetwork pullPreview, previewSource;
        private int previewNodeCount, previewEdgeCount;
        public NeuronNetwork PullPreview => pullPreview != null && controller != null && controller.IsOpen &&
            !controller.Growth.IsGrowing && previewSource == controller.Network &&
            previewNodeCount == controller.Network.Nodes.Count && previewEdgeCount == controller.Network.Edges.Count ? pullPreview : null;
        public string Feedback { get; private set; }
        [SerializeField] private MinigameController controller;
        [SerializeField] private CanvasPointerInput pointerInput;
        private PullSelection selection;
        private bool cutting;

        private void OnEnable()
        {
            if (controller == null || pointerInput == null)
            {
                Debug.LogError("Bind controller and pointer input in the Inspector.", this);
                enabled = false;
                return;
            }
            pointerInput.PointClicked += Press;
            pointerInput.PointDragged += Drag;
            pointerInput.PointReleased += Release;
        }

        private void OnDisable()
        {
            if (pointerInput != null)
            {
                pointerInput.PointClicked -= Press;
                pointerInput.PointDragged -= Drag;
                pointerInput.PointReleased -= Release;
            }
            CancelDrag();
        }

        public void SelectTool(Tool tool) { CancelDrag(); SelectedTool = tool; Feedback = null; }
        public void CancelDrag() { IsDragging = false; cutting = false; SelectedNode = -1; pullPreview = null; previewSource = null; }

        private void Press(Vector2 position)
        {
            if (!controller.CanEdit) return;
            switch (SelectedTool)
            {
                case Tool.Attractor:
                    controller.PlaceAttractor(position);
                    return;
                case Tool.Pull:
                    if (controller.Growth.IsGrowing) { Feedback = "请等待生长结束后牵引。"; return; }
                    selection = PathPull.Select(controller.Network, position, 0.18f);
                    IsDragging = selection.IsValid;
                    SelectedNode = selection.NodeId;
                    PreviewPosition = selection.Position;
                    Feedback = IsDragging ? (selection.NodeId >= 0 && controller.Network.Nodes[selection.NodeId].IsCutEndpoint ?
                        "只拖动选中的切口；另一端保持不动。向另一切口靠近后松开可重新接合。" :
                        "拖动控制点后松开，两侧连接位置保持不动。") : "请选路径中部、紫色拐点或切断后的切口。";
                    return;
                case Tool.Cut:
                    if (controller.Growth.IsGrowing) { Feedback = "请等待生长结束后切断。"; return; }
                    cutting = true; IsDragging = true;
                    CutStart = position; PreviewPosition = position;
                    Feedback = "拖出一条切线，松开后切断穿过的路径。";
                    return;
                case Tool.Branch:
                    Feedback = controller.PlaceBranchZone(position)
                        ? "已放置分叉范围，生长端进入后分成两支。" : "当前关卡不能使用分叉。";
                    return;
                case Tool.Reshape:
                    if (controller.Growth.IsGrowing) { Feedback = "请等待生长结束后重塑。"; return; }
                    selection = PathPull.Select(controller.Network, position, 0.20f);
                    if (selection.NodeId > 0 && controller.ReshapeCorner(selection.NodeId, 6))
                        Feedback = "已将折角平滑为曲线。";
                    else Feedback = "请点击已有折角（起点与分叉点除外）。";
                    return;
            }
        }

        private void Drag(Vector2 position)
        {
            if (!IsDragging || !controller.CanEdit) return;
            PreviewPosition = position;
            pullPreview = null;
            if (!cutting && !controller.Growth.IsGrowing && PathPull.Apply(controller.Network, selection, position,
                controller.Configuration.JoinDistance, out var preview, out int movedNode))
            {
                pullPreview = preview;
                previewSource = controller.Network;
                previewNodeCount = previewSource.Nodes.Count;
                previewEdgeCount = previewSource.Edges.Count;
                PreviewPosition = preview.Nodes[movedNode].Position;
            }
        }

        private void Release(Vector2 position)
        {
            if (!IsDragging) return;
            if (cutting)
            {
                Feedback = controller.CutPath(CutStart, position) ? "已切断穿过的路径。" : "切线未碰到轨迹。";
                CancelDrag();
                return;
            }
            Feedback = controller.PullPath(selection, position) ? "牵引完成。" : "未修改：位置无变化或过于接近相邻节点。";
            CancelDrag();
        }
    }
}
