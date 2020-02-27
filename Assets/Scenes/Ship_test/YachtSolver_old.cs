﻿using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using UnityEngine;
using System;

// Движение МПО в гор плоск (1)
// Лукомский Чугунов Системы управления МПО (2)
// Гофман Маневрирование судна (3)

public class YachtSolver_old : MonoBehaviour
{
    [Header("Управление:")]
    [Range(-1.0f,1.0f)]
    public float engineValue = 0;           // -1..+1, для получения текущей мощности умножается на enginePower
    [Range(-35.0f, 35.0f)]
    public float ruderlValue = 0;           // угол поворота пера руля

    [Header("Разные исходные данные:")]
    public float enginePower = 30000f;      // 40 л.с примерно 30 КВт
    public float maxV = 4.1f;               // 4.1 м/с = 8 узлов
    public float Ca = 0.97f;                // адмиралтейский коэффициент после перевода рассчетов в Си
    public float Lbody = 11.99f;            // длинна корпуса
    public float M0 = 8680f;                // водоизмещение = вес яхты в кг
    public float Jy = 35000f;               // момент инерции относительно вертикальной оси
    public float K11 = 0.3f;                // коеф. для расчета массы с учетом присоединенной Mzz = (1+K11)*M0
    public float K66 = 0.5f;                // коеф. для расчета массы и момента с учетом прис.  Jyy = (1+K66)*Jy; Mxx = (1+K66)*M0
    public float Krud = 0.19f;              // = 0.5*p*Sруля; Тогда сила на руле curFrud = Krud*V*V*KoefRud(curBeta,ruderlValue)
    public float KFrudX = 0.5f;             // Подгонка, для реализма эффективности руля
    public float KBeta = 0.5f;              // Чтобы уменьшить влияние Beta, так как руль обдувается водой винта (3 стр 55)

    // занос
    public int DirectV = 1;                 // направление вращения, +1 - правый винт, -1 - левый;
    public float Kzanos = 3;                // подбором, влияет на силу заноса кормы
    public float Tzanos = 1.5f;             // подбором, влияет на время (обратно) действия силы заноса после увеличения мощности 

    // рассчитывается один раз
    private float _KresZ;                   // коэффициент перед V*V при рассчете силы сопротивления по Z
    private float _KresX;                   // коэффициент перед V*V при рассчете силы сопротивления по X
    private float _Mzz;                     // Масса с учетом присоединенной
    private float _Mxx;                     // Масса с учетом присоединенной
    private float _Jyy;                     // Момент с учетом присоединенной массы

    // рассчитывается на каждом шаге
    [Header("Вывод для контроля:")]
    public float Feng;                      // сила тяги, будет получатся из curEngineP делением на maxV (?)
    public float FresZ;                     // сила сопротивления корпуса продольная
    public float FresX;                     // сила сопротивления корпуса поперечная
    public float Frud;                      // сила на руле  Krud * V*V  * KoefRud( Beta, ruderlValue )
    public float FrudZ;                     // доп. сила сопротивления из-за поворота руля 
    public float FrudX;                     // боковая сила из-за поворота руля 
    public float Mrud;                      // Момент возникающий на руле
    public float MresBody;                  // Момент сил сопротивления воды
    public float FengX;                     // боковая сила от винта, возникает при изменениях мощности
    public float MengX;                     // Момент от винта
    public float Vz = 0;                    // текущая продольная скорость
    public float Vx = 0;                    // боковая скорость
    public float OmegaY = 0;                // скорость поворота вокруг вертикальной оси
    public float Beta = 0;                  // угол между локальной осью OZ и скоростью

    void Start()
    {
        // рассчет коэффициента перед силой сопротивления в ур. динамики (1-8)
        _KresZ = Mathf.Pow( M0, 2.0f / 3.0f ) / Ca;
        _KresX = _KresZ * 20;               // примерно, с учетом отношения площадей сечений
         // массы и момент инерции с учетом присоединенных масс
        _Mzz = (1 + K11) * M0;
        _Mxx = (1 + K66) * M0;
        _Jyy = (1 + K66) * Jy;
    }

    private void Update()
    {
        if (Input.GetKeyDown("up"))
            engineValue += 0.1f;
        if (Input.GetKeyDown("down"))
            engineValue -= 0.1f;

        engineValue =  Mathf.Clamp(engineValue, -1.0f, 1.0f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // сила тяги
        float FengOld = Feng; // для анализа, нужен ли занос кормы
        Feng = enginePower * engineValue / maxV; 
        
        // сила сопротивления корпуса
        FresZ = -Mathf.Sign(Vz) * _KresZ * Vz * Vz;
        FresX = -Mathf.Sign(Vx) * _KresX * Vx * Vx;  

        // силы и моменты на руле
        FrudZ = -Mathf.Sign(Vz) * FruderZ(ruderlValue + Beta, Vz);
        FrudX = -Mathf.Sign(ruderlValue + Beta) * Mathf.Sign(Vz) * FruderX(ruderlValue + Beta, Vz);
        Mrud =  -FrudX * Lbody / 2;

        // Момент - сопротивление вращательному движению. Не по (1), а из физических соображений
        MresBody = -Mathf.Sign(OmegaY) *_KresX * (OmegaY * Lbody)* (OmegaY * Lbody) / 8;
        //MresBody = _Mzz * OmegaY;

        // сила и момент от увеличения мощности двигателя
        float impactX = detectEngineImpact(FengOld, Feng);
        float dFengX = dt * (Kzanos * impactX - Tzanos * FengX);    // спадающая экспонента
        FengX += dFengX;
        MengX = -FengX * Lbody / 2;

        // Интегрируем dVz/dt - продольная скорость по Z
        float rotToVz = _Mxx * OmegaY * Vx;
        float dVz = dt * (Feng + FresZ + FrudZ + rotToVz) / _Mzz;

        // Интегрируем dVx/dt - боковая скорость по X
        float rotToVx = -_Mzz * OmegaY * Vz;
        float dVx = dt * (FrudX + FresX + FengX + rotToVx) / _Mxx;
        //print("FrudX = " + FrudX + "   FresX = " + FresX + "   rotToVy = " + rotToVx + "   dVx = "+ dVx + "   Vx = "+Vx);

        // Интегрируем dOmegaY/dt - момент вокруг вертикальной оси Y
        float dOmegaY = dt * (Mrud + MresBody + MengX + (_Mzz - _Mxx) * Vx * Vz) / _Jyy;

        // Новые скорости
        Vz += dVz;
        Vx += dVx;
        OmegaY += dOmegaY;

        // Рассчет и изменение положения
        Vector3 localV = new Vector3(Vx, 0, Vz);
        Vector3 globalV = transform.TransformVector(localV);
        Vector3 curPos = gameObject.transform.position;
        curPos += globalV * dt;
        gameObject.transform.position = curPos;

        // Рассчет и изменение угла
        Vector3 rot = transform.eulerAngles;
        rot.y += OmegaY * 180 / Mathf.PI * dt;
        transform.eulerAngles = rot;

        
        // определение угла между продольной осью и скоростью
        if (Vz == 0.0f && Vx == 0.0f)
        {
            Beta = 0;
        }
        else if (Vz == 0.0f && Vx != 0.0f )
        {
            Beta = 90 * Mathf.Sign(Vx);
        }
        else
        {
            Beta = Mathf.Atan(Vx / Vz) *180/Mathf.PI;
        }
        Beta *= KBeta;
        //Beta = 0;
        /*
         print("");
         print("Vz = " + Vz + "   Vx = " + Vx);
         print("Beta = "+Beta + "   ruder = "+ ruderlValue + "   ruder + Beta = " + (ruderlValue + Beta) );
         */

    }

    // сила сопротивления руля по оси Z
    private float FruderZ(float ang, float V)
    {
        float VV = 2.37f;           // если скорость 1,54 то V*V = 2,37
        float Fv3;                  // сила при скорости 3 узла 

        ang = Mathf.Abs(ang);
        Fv3 = 12.3f + ang * (-0.311f+ang*(0.103f + ang*0.011f));

        return Fv3 / VV * V * V;
    }

    // сила сопротивления руля по оси X, порождает боковую скорость и момент вращения
    private float FruderX(float ang, float V)
    {
        float VV = 2.37f;           // если скорость 1,54 то V*V = 2,37
        float Fv3;                  // сила при скорости 3 узла 

        ang = Mathf.Abs(ang);
        Fv3 = ang * (59.88f + ang * (-2.57f + ang * 0.029f) );

        return Fv3 / VV * V * V * KFrudX; // KFrudX - подгонка, чтобы уменьшить эффективность руля
    }

    // боковая сила приложеная к корме из-за увеличения мощности двигателя
    // TODO! не все ситуации рассмотрены!
    private float detectEngineImpact( float FengOld, float Feng)
    {
        float impact = 0;
        if (FengOld >= 0 && FengOld < Feng)     // мощность увеличили
        {
            if (Vz >= 0)                        // стоим или плывем вперед
            {
                impact = Kzanos * ( Feng - FengOld );
            }
        }
        if (FengOld <= 0 && FengOld > Feng)     // увеличили мощность назад
        {
            if (Vz <= 0)                        // стоим или движемся назад
            {
                impact = Kzanos * (Feng - FengOld);
            }
        }
        // учтем направление вращения винта
        return impact*DirectV;
    }


}
