using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using UnityEngine;
using System;

public class Yacht : MonoBehaviour
{
    public float enginePower = 30000f;      // 40 л.с примерно 30 КВт
    public float engineValue = 0;           // -1..+1, для получения текущей мощности умножается на enginePower
    public float maxV = 4.1f;               // 4.1 м/с = 8 узлов
    public float Ca = 0.97f;                // адмиралтейский коэффициент после перевода рассчетов в Си
    public float M0 = 8680f;                // водоизмещение = вес яхты в кг

    // рассчитывается один раз
    public float Kres;                      // коэффициент перед V*V при рассчете силы сопротивления

    // рассчитывается на каждом шаге
    public float curFres;                   // сила сопротивления
    public float curFeng;                   // сила тяги, будет получатся из curEngineP делением на maxV (?)
    public float curV=0;                    // текущая скорость

    void Start()
    {
        Kres = Mathf.Pow( M0, 2.0f / 3.0f ) / Ca;
    }

    void FixedUpdate()
    {
        // тяга
        curFeng = enginePower * engineValue / maxV;
        // сопротивление
        curFres = -Mathf.Sign(curV) * Kres* curV * curV;
        // рассчет скорости
        float dV = Time.fixedDeltaTime * (curFeng + curFres) / M0;
        curV += dV;
        // рассчет положения
        Vector3 localV = new Vector3(curV, 0, 0);
        Vector3 globalV = transform.TransformVector(localV);
        Vector3 curPos = gameObject.transform.position;
        curPos += globalV * Time.fixedDeltaTime;
        gameObject.transform.position = curPos;
    }
}
