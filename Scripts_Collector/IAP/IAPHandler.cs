using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.SceneManagement;

public class IAPHandler : MonoBehaviour
{
    public void OnPurchaseComplete(Product product)
    {
        if(Database.instance != null && Database.instance.isInitialized)
        {
            CompletePurchase(product);
        }
        else
        {
            StartCoroutine(WaitTillDatabaseInitialized(product));
        }
    }

    public void OnPurchaseFailure(Product product, PurchaseFailureReason reason)
    {
        Debug.Log("Failure: " + product.definition.id + " - " + reason.ToString());
    }

    private void CompletePurchase(Product product)
    {
        List<GrantedResource> resources = new List<GrantedResource>();

        // Check if its rubies or a pack
        if (product.definition.id.Contains("rubies"))
        {
            // Rubies are formatted as: rubies_x, where x is the amount of rubies
            double value = 0;
            double.TryParse(product.definition.id.Split('_')[1], out value);

            if (value > 0)
            {
                Database.instance.AddRubies(value);

                // Add IAP score, 1 point for every dollar spent
                switch (value)
                {
                    case 10:
                        Database.instance.IAPScore += 1;
                        break;
                    case 50:
                        Database.instance.IAPScore += 5;
                        break;
                    case 110:
                        Database.instance.IAPScore += 10;
                        break;
                    case 240:
                        Database.instance.IAPScore += 20;
                        break;
                    case 625:
                        Database.instance.IAPScore += 50;
                        break;
                    case 1300:
                        Database.instance.IAPScore += 100;
                        break;
                }

                resources.Add(new GrantedResource(Grant.RUBIES, value));
            }
        }
        else
        {
            SpecialPack pack = SpecialPack.GetSpecialPackForId(product.definition.id);

            if (pack != null)
            {
                pack.GrantPackContents();

                // Add IAP score, 1 point for every dollar spent
                if (pack.ID.Equals(SpecialPack.STARTER_PACK))
                {
                    Database.instance.IAPScore += 8;
                }
                else if (pack.ID.Equals(SpecialPack.MEGA_PACK))
                {
                    Database.instance.IAPScore += 30;
                }
                else
                {
                    Database.instance.IAPScore += 15;
                }

                foreach (SpecialPackContent content in pack.contents)
                {
                    resources.Add(content.ConvertToGrantedResource());
                }
            }
            else
            {
                Debug.Log("Unknown product bought: " + product.definition.id);
                return;
            }
        }

        if(SceneManager.GetActiveScene().buildIndex == Loading.SCENE_ID_GAME)
        {
            ShowResourcesBought(resources);
        }
        else
        {
            StartCoroutine(WaitTillGameLoaded(resources));
        }
    }

    private void ShowResourcesBought(List<GrantedResource> resources)
    {
        foreach (GameObject obj in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (obj.name.Equals("Game"))
            {
                obj.GetComponent<Game>()?.ShowResourcesEarned(resources, overrideTitle: Translator.GetTranslationForId(Translation_Script.PURCHASE_SUCCESS));
                obj.GetComponent<Game>()?.UpdateEverything();
            }
        }
    }

    private IEnumerator WaitTillDatabaseInitialized(Product product)
    {
        // Wait till DB is intialized
        yield return new WaitUntil(() => Database.instance != null && Database.instance.isInitialized);
        CompletePurchase(product);
    }

    private IEnumerator WaitTillGameLoaded(List<GrantedResource> resources)
    {
        // Wait till Game scene is opened
        yield return new WaitUntil(() => SceneManager.GetActiveScene().buildIndex == Loading.SCENE_ID_GAME);
        ShowResourcesBought(resources);
    }
}