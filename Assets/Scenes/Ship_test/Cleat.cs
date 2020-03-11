using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


public class Cleat : MonoBehaviour
{
    //public Rope[] Ropes;
    public List<Rope> Ropes;
    public bool drawRope = false;
    public GameObject prefabVisualRope;

    private List<GameObject> _visualRopes;

    private void Start()
    {
        _visualRopes = new List<GameObject>();
        // создать и нарисовать канаты
        for (int i = 0; i < Ropes.Count; i++)
        {
            GameObject newRope = GameObject.Instantiate(prefabVisualRope);
            newRope.transform.parent = transform;   // родитель - утка
            newRope.transform.localPosition = Vector3.zero;
            //Ropes[i].visualRope = newRope;
            _visualRopes.Add(newRope);
            drawOneRope(i);
        }
    }

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
            drawOneRope(i);
        }
        // TODO! если несколько канатов от этой утки лопнули одновременно, надо делать по другому
        if(numDel != -1)
        {
            Ropes.RemoveAt(numDel);
            Destroy(_visualRopes[numDel]);
            _visualRopes.RemoveAt(numDel);
        }
    }

    private void drawOneRope(int i)
    {
        Rope r = Ropes[i];
        LineRenderer lr = _visualRopes[i].GetComponent<LineRenderer>();
        Vector3[] points = new Vector3[2];
        points[0] = transform.position;
        points[1] = r.Pos;
        lr.positionCount = 2;
        lr.SetPositions(points);
        Color col = detectRopeColor(r);
        lr.startColor = col;
        lr.endColor = col;
    }

    // отладочное рисование каната в виде линии
    private void OnDrawGizmos()
    {
        if (drawRope)
        {
            for (int i = 0; i < Ropes.Count; i++)
            {
                Color col = detectRopeColor(Ropes[i]);
                Gizmos.color = col;
                Gizmos.DrawLine(transform.position, Ropes[i].Pos);
            }
        }
    }

    private Color detectRopeColor(Rope r)
    {
        float curDist = Vector3.Distance(transform.position, r.Pos);
        Color col = Color.black;
        if (curDist < r.Len)
        {
            // канат не натянут
            col = Color.black;
        }
        else if (curDist < r.Len * r.Stretch)
        {
            // растяжение в допустимых пределах 
            float blueAdd = 0;
            float maxL = r.Len * r.Stretch;
            blueAdd = (curDist - r.Len) / (maxL - r.Len) * 0.5f;
            col = new Color(0.5f, 0.5f + blueAdd, 0.5f);
        }
        else
        {
            // превышено максимальное растяжение
            col = new Color(1.0f, 0.5f, 0.5f);
        }
        return col;
    }
}



[Serializable]
public struct Rope
{
    public Vector3 Pos;
    public float Len;
    public float Stretch;
    public float MaxForce;
    public GameObject visualRope;
    //internal LineRenderer lr;
}

