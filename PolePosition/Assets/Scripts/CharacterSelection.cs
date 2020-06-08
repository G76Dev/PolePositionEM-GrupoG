using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterSelection : MonoBehaviour
{
    //lista donde almacenar los objetos
    public List<GameObject> models = new List<GameObject>();

    //establecemos la seleccion de los objetos de acuerdo a un numero
    //el modelo en la posicion 0 sera el modelo que aparecera por defecto
    public int selection = 0;

    
    // Start is called before the first frame update
    private void Start()
    {
        foreach(GameObject car in models)
        {
            car.SetActive(false);
        }
        models[selection].SetActive(true);
    }

    // Update is called once per frame
    private void Update()
    {

    }

    public void back()
    {
        foreach (GameObject car in models)
        {
            car.SetActive(false);
        }
        selection--;
        if (selection < 0)
        {
            selection = models.Count - 1;
        }
        models[selection].SetActive(true);
    }

    public void next()
    {
        foreach (GameObject car in models)
        {
            car.SetActive(false);
        }
        selection++;
        if (selection > models.Count - 1)
        {
            selection = 0;
        }
        models[selection].SetActive(true);
    }
}
