using UnityEngine;
using System.Collections;

public class NPCDialogUI : MonoBehaviour
{
    [SerializeField] private NPCAgent npcAgent;
    [SerializeField] private string npcName = "NPC";
    [SerializeField] private float characterDelay = 0.03f;

    private string _dialogText;
    private bool _isTyping;

    private void Awake()
    {
        if (npcAgent == null)
            npcAgent = FindObjectOfType<NPCAgent>();
    }

    private void OnEnable()
    {
        npcAgent.OnDialog += HandleDialog;
    }

    private void OnDisable()
    {
        npcAgent.OnDialog -= HandleDialog;
    }

    private void HandleDialog(string text)
    {
        StopAllCoroutines();
        StartCoroutine(TypeDialog(text));
    }

    private IEnumerator TypeDialog(string text)
    {
        _isTyping = true;
        _dialogText = "";

        foreach (char c in text)
        {
            _dialogText += c;
            yield return new WaitForSeconds(characterDelay);
        }

        _isTyping = false;
    }

    private void OnGUI()
    {
        float boxWidth = Screen.width * 0.7f;
        float boxHeight = 140f;
        float x = (Screen.width - boxWidth) / 2f;
        float y = Screen.height - boxHeight - 20f;

        GUILayout.BeginArea(new Rect(x, y, boxWidth, boxHeight), GUI.skin.box);

        GUILayout.Label($"<b>{npcName}</b>", GUILayout.ExpandWidth(true));

        if (!string.IsNullOrEmpty(_dialogText))
        {
            GUILayout.TextArea(_dialogText, GUILayout.ExpandHeight(true));
        }

        if (!_isTyping)
        {
            GUILayout.Label("[Press Space to continue]", GUILayout.ExpandWidth(false));
        }

        GUILayout.EndArea();
    }
}
