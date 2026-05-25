using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Disables XR Socket hover preview meshes that create large yellow blocks in VR.
/// Socket selection still works; only the visual preview is hidden.
/// </summary>
public sealed class XRSocketHoverMeshSuppressor : MonoBehaviour
{
    [SerializeField] private bool _disableEveryFrameForSafety = true;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        DisableHoverMeshes();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void LateUpdate()
    {
        if (_disableEveryFrameForSafety)
            DisableHoverMeshes();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableHoverMeshes();
    }

    public void DisableHoverMeshes()
    {
        foreach (XRSocketInteractor socket in FindObjectsByType<XRSocketInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (socket == null)
                continue;

            socket.showInteractableHoverMeshes = false;
            socket.interactableHoverScale = 1f;
        }
    }
}
