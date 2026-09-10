using UnityEngine;
using UnityEngine.UI;

public class CharacterActionList : MonoBehaviour
{

    [SerializeField] private bool isSelected = false;
    [SerializeField] private CharacterActionType actionType;

    public enum CharacterActionType
    {
        Move,
        Interact,
        Sig,
        Other
    }
    public Sprite[] sprites;

    private Image image;

    private void Start()
    {
        // ตั้งค่าปุ่ม
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnIconClicked);
        }

        image = GetComponent<Image>();
        if (image != null && sprites != null && sprites.Length > 0)
        {
            image.sprite = sprites[0];
        }

        isSelected = false;
    }

    public void Setup(GameObject target)
    {
        return;
    }

    public void OnIconClicked()
    {

        //Mockup
        //Tell GameManager that the player has selected this action
        InGameUIManager.Instance.SelectAction((int)actionType);
        isSelected = !isSelected;

    }
    public void Selected()
    {
        isSelected = true;
    }
    public void Unselected()
    {
        isSelected = false;
    }
    public void Update()
    {
        if (isSelected)
        {
            image.sprite = sprites[1];
        }
        else
        {
            image.sprite = sprites[0];
        }
    }
}
