using UnityEngine;

public sealed class PageReward : MonoBehaviour
{
    public Transform lid;
    public bool InReach { get; private set; }
    public bool Used { get; private set; }
    public void Consume()
    {
        Used = true;
        if (lid) lid.localRotation = Quaternion.Euler(68,0,0);
        else gameObject.SetActive(false);
    }
    void OnTriggerStay(Collider other) { if (other.attachedRigidbody && other.attachedRigidbody.CompareTag("Player")) InReach = true; }
    void OnTriggerExit(Collider other) { if (other.attachedRigidbody && other.attachedRigidbody.CompareTag("Player")) InReach = false; }
}
