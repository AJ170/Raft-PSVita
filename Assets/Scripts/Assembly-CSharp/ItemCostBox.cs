using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemCostBox : MonoBehaviour
{
	public TextMeshProUGUI costLabel;

	public Image itemImage;

	private Cost cost;

	private void Update()
	{
		if (cost != null && base.gameObject.activeInHierarchy)
		{
			costLabel.text = PlayerInventory.Singleton.GetItemCount(cost.item.index).ToString() + "/" + cost.amount;
		}
	}

	public void SetCost(Cost cost)
	{
		this.cost = cost;
		itemImage.sprite = cost.item.sprite;
		costLabel.text = PlayerInventory.Singleton.GetItemCount(cost.item.index).ToString() + "/" + cost.amount;
	}
}
