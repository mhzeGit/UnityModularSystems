// Manages the object pool of InputPromptView instances and assigns them to the correct screen anchor (left, center, or right). Handles view lifecycle — getting a pooled or newly-instantiated view, releasing views back to the pool, releasing all views, and trimming the pool to a maximum size to prevent memory leaks.

using System.Collections.Generic;
using UnityEngine;

namespace MHZE.InputPromptSystem
{
public class InputPromptUI : MonoBehaviour
{
    private const int MaxPoolSize = 20;

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
            if (promptPool.Count >= MaxPoolSize)
            {
                Debug.LogWarning($"[InputPromptUI] Pool has reached maximum size ({MaxPoolSize}). Cannot create new view.");
                return null;
            }

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

        TrimPool();
    }

    private void TrimPool()
    {
        while (promptPool.Count > MaxPoolSize)
        {
            var index = promptPool.Count - 1;
            var excess = promptPool[index];
            if (excess != null)
            {
                Destroy(excess.gameObject);
            }
            promptPool.RemoveAt(index);
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
