using System.Collections.Generic;
using UnityEngine;

namespace MHZE.InputPromptSystem
{
public class InputPromptUI : MonoBehaviour
{
    [SerializeField] private InputPromptView promptPrefab;
    [SerializeField] private RectTransform leftAnchor;
    [SerializeField] private RectTransform centerAnchor;
    [SerializeField] private RectTransform rightAnchor;

    private readonly List<InputPromptView> promptPool = new();

    public InputPromptView GetView(InputPromptLocation location)
    {
        var parent = GetAnchor(location);
        if (parent == null)
        {
            Debug.LogWarning($"[InputPromptUI] Missing anchor for location {location}.");
            return null;
        }

        InputPromptView view = null;
        for (var i = 0; i < promptPool.Count; i++)
        {
            var pooled = promptPool[i];
            if (pooled != null && !pooled.gameObject.activeInHierarchy)
            {
                view = pooled;
                break;
            }
        }

        if (view == null)
        {
            if (promptPrefab == null)
            {
                Debug.LogError("[InputPromptUI] Prompt prefab is not assigned.");
                return null;
            }

            view = Instantiate(promptPrefab, transform);
            promptPool.Add(view);
        }

        view.gameObject.SetActive(true);
        view.transform.SetParent(parent, false);
        return view;
    }

    public void ReleaseView(InputPromptView view)
    {
        if (view == null)
        {
            return;
        }

        view.gameObject.SetActive(false);
    }

    public void ReleaseAllViews()
    {
        for (var i = 0; i < promptPool.Count; i++)
        {
            if (promptPool[i] != null)
            {
                promptPool[i].gameObject.SetActive(false);
            }
        }
    }

    public RectTransform GetAnchor(InputPromptLocation location)
    {
        return location switch
        {
            InputPromptLocation.Left => leftAnchor,
            InputPromptLocation.Right => rightAnchor,
            _ => centerAnchor
        };
    }
}
}
