using UnityEngine;
using UnityEngine.UI;

public class ObjectIcon : MonoBehaviour
{
    [SerializeField] private bool isSelected = false;
    public GameObject TargetObject { get; private set; }

    //[0] = สถานะปกติ, [1] = สถานะถูกเลือก
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
        TargetObject = target;

        // if (image != null) image.sprite = target.GetComponent<ObjectData>().iconSprite;
    }

    public void OnIconClicked()
    {
        //บอก GameManager ว่าผู้เล่นเลือก Object ตัวนี้

        //Mockup
        isSelected = !isSelected;
        InGameUIManager.Instance.ShowActionList(TargetObject);
        //InGameUIManager.Instance.SelectCharacter(TargetObject);

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