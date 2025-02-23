using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Quantum;

public class HazardMenuManager : MonoBehaviour {
    [SerializeField] private GameObject NewHazardPrompt;

  //My Best Friend Variables
    [SerializeField] private Transform ButtonList;
    [SerializeField] private GameObject ButtonTemplate;
    [SerializeField] private TMP_Text ButtonLabel;

  //Item Data List Variables
    [SerializeField] private Transform ContentList;
    [SerializeField] private GameObject CheckTemplate, SliderTemplate, OptionTemplate;
    [SerializeField] private TMP_Text CheckLabel, SliderLabel, OptionLabel;
    private List<GameObject> DataListContents = new();

  //Default Values (Temporary)
    public List<(string, List<(string, string, int)>)> DefaultValue = new List<(string, List<(string, string, int)>)>() {
/*    ("Template", new List<(string, string, int)>(
        ("Check", "Name", 0),
        ("Slider", "Name", 0),
        ("Option", "Name|OptionA|OptionB|OptionC", 0),
        ("Barrier", "Name", 0),
      )),*/
      ("Coinbunch", new List<(string, string, int)>() {
          ("Slider", "Scale", 100),
          ("Check", "Spawn From World", 1),
          ("Check", "Spawn From Fridge", 1),
            ("Barrier", "Data", 0),
          ("Slider", "Coins", 3),
          ("Slider", "LifeTime", 7)
      }),
      ("Koopa", new List<(string, string, int)>() {
          ("Slider", "LifeTime", 100),
          ("Slider", "Scale", 100),
          ("Check", "Spawn From World", 1),
          ("Check", "Spawn From Bulb", 1),
          ("Check", "Spawn From Fridge", 1),
            ("Barrier", "Data", 0),
          ("Slider", "InShell", 3),
          ("Slider", "LifeTime", 7)
      }),
      ("Heavy Stone", new List<(string, string, int)>() {
          ("Slider", "LifeTime", 100),
          ("Slider", "Scale", 100),
          ("Check", "Spawn From World", 1),
          ("Check", "Spawn From Fridge", 1),
          ("Check", "Spawn From Bulb", 1),
      }),
      ("Sleek Table", new List<(string, string, int)>() {
          ("Slider", "Sleekness", 100),
          ("Slider", "Staleness", 100),
          ("Check", "Is Actual Table", 1),
          ("Check", "Are We Sure Is Actual Table", 1),
          ("Check", "Table?", 1),
          ("Option", "MyTable... :D|OptionA|OptionB|OptionC", 0),
          ("Check", "Made of Stainless Steel", 1),
          ("Check", "Tabby Cats?", 1),
          ("Slider", "Cat's On Top", 100),
          ("Slider", "Copies Sold", 100),
          ("Check", "Is Bonus Copy", 1),
          ("Check", "Did Eat My Father?", 1),
          ("Check", "Is This All A Joke To Showcase Scrollability?", 1),
      }),
    };
    public List<(string, List<(string, string, int)>)> Items = new List<(string, List<(string, string, int)>)>() {};

// ------ General Raw Update
//TODO: Properly Update The Values

// ------ New Hazard Prompt
//TODO: Add Hazard Menu Automated Buttons
    public void OpenHazardPrompt() {
      NewHazardPrompt.SetActive(true);
    }

// ------ List Ui Buttons
    public void AddHazard(int i) {
      NewHazardPrompt.SetActive(false);
      GameObject newTemplate = Instantiate(ButtonTemplate);
      newTemplate.SetActive(true);
      newTemplate.transform.SetParent(ButtonList, false);
      newTemplate.name = "" + Items.Count;
      foreach(Transform trans in newTemplate.transform) {
        trans.name = "" + Items.Count;
      }
      newTemplate.GetComponentInChildren<TMP_Text>().text = DefaultValue[i].Item1;
      Items.Add(DefaultValue[i]);
    }
    public void DeleteHazard(GameObject btnMaster) {
      foreach (GameObject Kill in DataListContents)
        Destroy(Kill);
      int.TryParse(btnMaster.gameObject.name, out int o);
      int i = o;
      foreach(Transform f in ButtonList) {
        if (int.TryParse(f.gameObject.name, out int j) && j == i)
          Destroy(f.gameObject);
        if (j > i) //Item Name Larger Than Removed
          f.gameObject.name = "" + (j - 1);
      }
      Items.RemoveAt(i);
      ResetDataButtons();
    }

    public void SetValue() {
    }

// ------ Data Ui Elements
    public void ResetDataButtons() {
        //Reset preview
        foreach (GameObject Kill in DataListContents)
          Destroy(Kill);
    }

    public void CreateButtonsFrom(GameObject btnMaster) {
        int ItemID = int.Parse(btnMaster.name);

        ResetDataButtons();

        GameObject newTemplate = null;
        for (int i = 0; i < Items[ItemID].Item2.Count; i++) {
          if (Items[ItemID].Item2[i].Item1 == "Check") {
            CheckLabel.text = Items[ItemID].Item2[i].Item2;
            newTemplate = Instantiate(CheckTemplate);
          } else if (Items[ItemID].Item2[i].Item1 == "Slider") {
            SliderLabel.text = Items[ItemID].Item2[i].Item2;
            newTemplate = Instantiate(SliderTemplate);
          } else if (Items[ItemID].Item2[i].Item1 == "Option") {
            OptionLabel.text = Items[ItemID].Item2[i].Item2;
            newTemplate = Instantiate(OptionTemplate);
          }

          if (newTemplate != null) {
            newTemplate.SetActive(true);
            newTemplate.transform.SetParent(ContentList, false);
            DataListContents.Add(newTemplate);
            newTemplate.name = "" + ItemID;

            newTemplate = null;
          }
        }
        return;
    }
}
