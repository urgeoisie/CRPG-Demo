using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MirrorCase2D
{
    public sealed class SceneHotspot : MonoBehaviour
    {
        public string title;
        [TextArea(3, 12)] public string body;
        public string prompt = "E 交互";
        public string evidence;
        public string[] choiceLabels;
        [TextArea(2, 8)] public string[] choiceResponses;
        public string[] choiceEvidence;
        public string[] choiceTargetRooms;
        public Vector2[] choiceTargetLocalPositions;
        public Transform outline;

        public bool HasChoices => choiceLabels != null && choiceLabels.Length > 0;

#if UNITY_EDITOR
        private static readonly Color DevFill = new Color(1f, 0.55f, 0.05f, 0.12f);
        private static readonly Color DevLine = new Color(1f, 0.55f, 0.05f, 0.85f);

        private void OnDrawGizmos()
        {
            DrawDeveloperHotspot(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawDeveloperHotspot(true);
        }

        private void DrawDeveloperHotspot(bool selected)
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box == null) return;

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = selected ? new Color(1f, 0.85f, 0.05f, 0.22f) : DevFill;
            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = selected ? Color.yellow : DevLine;
            Gizmos.DrawWireCube(box.offset, box.size);
            Gizmos.matrix = previous;

            if (!string.IsNullOrEmpty(title))
            {
                Vector3 labelPosition = transform.TransformPoint(box.offset + Vector2.up * (box.size.y * 0.5f + 0.18f));
                Handles.Label(labelPosition, title);
            }
        }
#endif
    }
}
