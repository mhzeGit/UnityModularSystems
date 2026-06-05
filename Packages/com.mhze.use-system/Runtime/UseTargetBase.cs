using System;
using System.Collections.Generic;
using UnityEngine;
using ArgEvent;

namespace MHZE.UseSystem
{
    public class UseTargetBase : MonoBehaviour, IUsableTarget
    {
        [SerializeField] bool canUseAtTarget = true;

        [System.Serializable]
        public class ToolData
        {
            public ToolId acceptedToolId;
            public string usePromptPrefix = "Press";
            public string usePromptSuffix = "To Use!";
        }
        public TargetId targetId;
        public List<ToolData> toolData;

        public ArgEventBinding OnUsedAtTargetEvent = new ArgEventBinding();
        public event Action<GameObject, string, RaycastHit> OnUsedAtTarget;

        List<string> cachedAcceptedToolIds;
        bool isDirty = true;

        void OnValidate()
        {
            isDirty = true;
        }

        void OnEnable()
        {
            isDirty = true;
        }

        public bool GetCanUseAtTarget() => canUseAtTarget;

        public void SetCanUseAtTarget(bool Bool)
        {
            canUseAtTarget = Bool;
        }

        public IReadOnlyList<string> GetAcceptedToolIds()
        {
            if (isDirty)
            {
                cachedAcceptedToolIds ??= new List<string>();
                cachedAcceptedToolIds.Clear();
                foreach (ToolData data in toolData)
                {
                    cachedAcceptedToolIds.Add(data.acceptedToolId.value);
                }
                isDirty = false;
            }
            return cachedAcceptedToolIds;
        }

        public string GetUsePromptPrefix(string toolId)
        {
            foreach (var data in toolData)
            {
                if (data.acceptedToolId.value == toolId)
                {
                    return data.usePromptPrefix;
                }
            }
            return string.Empty;
        }

        public string GetUsePromptSuffix(string toolId)
        {
            foreach (var data in toolData)
            {
                if (data.acceptedToolId.value == toolId)
                {
                    return data.usePromptSuffix;
                }
            }
            return string.Empty;
        }
        public string GetTargetId() => targetId.value;

        public virtual void Used(GameObject usedBy, string usedToolId, RaycastHit HitResults)
        {
            OnUsedAtTarget?.Invoke(usedBy, usedToolId, HitResults);
            OnUsedAtTargetEvent.Invoke();
        }
    }
}
