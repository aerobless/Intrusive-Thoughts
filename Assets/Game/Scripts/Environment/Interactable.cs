using UnityEngine;

public class Interactable : MonoBehaviour, IDescribable
{
    [SerializeField, TextArea(5, 20)]
    public string description;

    public string Description => description;
    public string Name => name;
}
