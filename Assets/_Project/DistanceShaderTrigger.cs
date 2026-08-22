using UnityEngine;

public class DistanceShaderTrigger : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Material targetMaterial;

    private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerPosition");

    private void Update()
    {
        if (playerTransform != null && targetMaterial != null)
        {
            // Pass the player's world position to the shader graph
            targetMaterial.SetVector(PlayerPosID, playerTransform.position);
        }
    }
}