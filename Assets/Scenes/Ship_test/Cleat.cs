using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


public class Cleat : MonoBehaviour
{
    //public Rope[] Ropes;
    public List<Rope> Ropes;
    public bool drawRope = false;

    public Vector3 getForce()
    {
        Vector3 summF = Vector3.zero;

        for (int i = 0; i < Ropes.Count; i++)
        {
            float curDist = Vector3.Distance(transform.position, Ropes[i].Pos);
            if (curDist > Ropes[i].Len && curDist < Ropes[i].Len * Ropes[i].Stretch )
            {
                float valueF = (curDist / Ropes[i].Len - 1) / (Ropes[i].Stretch - 1) * Ropes[i].MaxForce; ;
                Vector3 oneF = (Ropes[i].Pos - transform.position).normalized * valueF;
                summF += oneF;
            }
        }
        return summF;
    }

    void FixedUpdate()
    {
        int numDel=-1;
        // анализ натяжения канатов
        for (int i = 0; i < Ropes.Count; i++)
        {
            float curDist = Vector3.Distance(transform.position, Ropes[i].Pos);
            if (curDist / Ropes[i].Len > Ropes[i].Stretch)
            {
                print("Канат лопнул! Утка " + gameObject.name + "   канат " + i);
                numDel = i;
            }
        }
        // TODO! если несколько канатов от этой утки лопнули одновременно, надо делать по другому
        if(numDel != -1)
        {
            Ropes.RemoveAt(numDel);
        }

    }

    // отладочное рисование каната в виде линии
    private void OnDrawGizmos()
    {
        if (drawRope)
        {
            for (int i = 0; i < Ropes.Count; i++)
            {
                float curDist = Vector3.Distance(transform.position, Ropes[i].Pos);
                Color col = Color.black;
                if ( curDist < Ropes[i].Len )      
                {
                    // канат не натянут
                    col = Color.black;
                }
                else if(curDist < Ropes[i].Len * Ropes[i].Stretch)
                {
                    // растяжение в допустимых пределах 
                    float blueAdd = 0;
                    float maxL = Ropes[i].Len * Ropes[i].Stretch;
                    blueAdd = (curDist - Ropes[i].Len) / (maxL - Ropes[i].Len)*0.5f;
                    col = new Color(0.5f, 0.5f + blueAdd, 0.5f);
                }
                else
                {
                    // превышено максимальное растяжение
                    col = new Color(1.0f, 0.5f, 0.5f);
                }
                Gizmos.color = col;
                Gizmos.DrawLine(transform.position, Ropes[i].Pos);
            }
        }
    }
}



[Serializable]
public struct Rope
{
    public Vector3 Pos;
    public float Len;
    public float Stretch;
    public float MaxForce;
}

