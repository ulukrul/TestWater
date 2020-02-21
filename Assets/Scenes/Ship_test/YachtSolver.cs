using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using UnityEngine;
using System;

// Движение МПО в гор плоск (1)
// Лукомский Чугунов Системы управления МПО (2)
// 

public class YachtSolver : MonoBehaviour
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
    public float FrudZ;                     // доп. сила сопротивления из-за поворота руля = - Frud*sin( abs(ruderlValue) )
    public float FrudX;                     // боковая сила из-за поворота руля = Frud*Cos(ruderlValue);
    public float Mrud;                      // Момент возникающий на руле
    public float MresBody;                  // Момент сил сопротивления воды
    public float Meng;                      // Момент от винта
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
        Feng = enginePower * engineValue / maxV; 
        
        // сила сопротивления корпуса
        FresZ = -Mathf.Sign(Vz) * _KresZ * Vz * Vz;
        FresX = -Mathf.Sign(Vx) * _KresX * Vx * Vx;  

        // силы и моменты на руле
        FrudZ = -Mathf.Sign(Vz) * FruderZ(ruderlValue + Beta, Vz);
        FrudX = -Mathf.Sign(ruderlValue + Beta) * FruderX(ruderlValue + Beta, Vz);
        Mrud =  -FrudX * Lbody / 2;

        // Момент - сопротивление вращательному движению. Не по (1), а из физических соображений
        MresBody = -Mathf.Sign(OmegaY) *_KresX * (OmegaY * Lbody)* (OmegaY * Lbody) / 8;
        //MresBody = _Mzz * OmegaY;


        // Интегрируем dVz/dt - продольная скорость по Z
        float rotToVz = _Mxx * OmegaY * Vx;
        float dVz = dt * (Feng + FresZ + FrudZ + rotToVz) / _Mzz;

        // Интегрируем dVx/dt - боковая скорость по X
        float rotToVx = -_Mzz * OmegaY * Vz;
        float dVx = dt * (FrudX + FresX + rotToVx) / _Mxx;
        //print("FrudX = " + FrudX + "   FresX = " + FresX + "   rotToVy = " + rotToVx + "   dVx = "+ dVx + "   Vx = "+Vx);

        // Интегрируем dOmegaY/dt - момент вокруг вертикальной оси Y
        float dOmegaY = dt * (Mrud + MresBody + (_Mzz - _Mxx) * Vx * Vz) / _Jyy;

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
        //Beta = 0;
       
        print("");
        print("Vz = " + Vz + "   Vx = " + Vx);
        print("Beta = "+Beta + "   ruder = "+ ruderlValue + "   ruder + Beta = " + (ruderlValue + Beta) );
        
        
    }

    // сила сопротивления руля по оси Z
    private float FruderZ(float ang, float V)
    {
        float[] angles = { 0, 4, 8, 12, 16, 18, 35 };
        float[] forces = { 10, 18, 23, 38, 75, 110, 600 };

        return RuderAbsForce(ang, V, angles, forces);
    }

    // сила сопротивления руля по оси X, порождает боковую скорость и момент вращения
    private float FruderX(float ang, float V)
    {
        float[] angles = { 0, 4, 8, 12, 16, 18, 35 };
        float[] forces = { 0, 180, 320, 410, 415, 400, 200 };

        return RuderAbsForce( ang,  V, angles, forces)/2;   // Деление на 2 - подгонка, чтобы уменьшить эффективность руля
    }

    private float RuderAbsForce(float ang, float V, float[] angles, float[] forces)
    {
        float Fv3 = 0;              // сила при скорости 3 узла 
        float VV = 2.37f;           // если скорость 1,54 то V*V = 2,37
        int idx1, idx2;

        ang = Mathf.Abs(ang);
       
        if (ang == angles[0])
        {
            Fv3 = forces[0];
        }
        else if ( ang >= angles[angles.Length - 1] )
        {
            Fv3 = forces[forces.Length - 1];
        }
        else
        {
            idx1 = idx2 = -1;      // подстраховка
            for (int i = 1; i < angles.Length; i++)
            {
                if (angles[i] >= ang)
                {
                    idx1 = i - 1;
                    idx2 = i;
                    break;
                }
            }
            if (idx1 == -1)
            {
                Fv3 = forces[0];
                print("Неправильно определяется диапазон для силы Fruder");
            }
            else
            {
                Fv3 = forces[idx1] + (forces[idx2] - forces[idx1]) * (ang- angles[idx1]) / (angles[idx2] - angles[idx1]);
            }
        }
        //print("ang = " + ang + "Frud = " + Fv3 / VV * V * V );
        return Fv3 / VV * V * V;

    }

}
