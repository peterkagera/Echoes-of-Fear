using UnityEngine;

public class DistanceShaderTrigger : MonoBehaviour
{
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Material targetMaterial;

    private static readonly int PlayerPosID = Shader.PropertyToID("_PlayerPosition");
    private Vector3 lastPosition;

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }
    }

    private void Update()
    {
        if (playerTransform != null && targetMaterial != null)
        {
            Vector3 currentPos = playerTransform.position;
            // Only send vector to material when player position actually changes
            if ((currentPos - lastPosition).sqrMagnitude > 0.001f)
            {
                targetMaterial.SetVector(PlayerPosID, currentPos);
                lastPosition = currentPos;
            }
        }
    }
}