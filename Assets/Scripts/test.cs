using UnityEngine;
using MHZE.InteractSystem;

public class test : MonoBehaviour
{
    
    public void DebugFloat(float number)
    {
    
            Debug.Log(number);
    }
    public void DebugFloat2()
    {
    
            Debug.Log(2);
    }
    public void InteractTest(IInteractor interactor)
    {
    
            Debug.Log(interactor.PlayerCamera.name);
    }
    public void InteractTestAndFloat(IInteractor interactor,float number)
    {
    
            Debug.Log(interactor.PlayerCamera.name);
            Debug.Log(number);
    }
}
