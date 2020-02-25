using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Buyki : MonoBehaviour
{
    public float interval=10f;
    public GameObject buyExample;

    void Start()
    {
        GameObject allBuis = new GameObject();
        allBuis.name = "All Buis";
        Instantiate(allBuis, transform.parent);

        float curX = -gameObject.transform.localScale.x * 5 + interval / 2;
        while ( curX < gameObject.transform.localScale.x * 5 )
        {
            float curZ = -gameObject.transform.localScale.z * 5 + interval / 2;
            while (curZ < gameObject.transform.localScale.z * 5 )
            {
                GameObject oneBuy = Instantiate(buyExample, allBuis.transform);
                Color с = new Color(0.5f+Random.value/2, 0.5f + Random.value / 2, 0.5f + Random.value / 2);
                oneBuy.GetComponent<Renderer>().material.color = с;
                oneBuy.name = "Buy_" + (int)curX + "_" + (int)curZ;
                oneBuy.transform.localPosition = new Vector3(curX, 0, curZ);
                curZ += interval;
                //print(curX+","+ curZ);
            }
            curX += interval;
        }
    }

}
