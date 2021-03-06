﻿using System.Collections;
using System.Collections.Generic;
using System.Xml.Schema;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization;

// Движение МПО в гор плоск (1)
// Лукомский Чугунов Системы управления МПО (2)
// Гофман Маневрирование судна (3)

public class YachtSolver : MonoBehaviour
{
    [Header("Управление:")]
    [Range(-1.0f, 1.0f)]
    public float engineValue = 0;           // -1..+1, для получения текущей мощности умножается на enginePower
    [Range(-540.0f, 540.0f)]
    public float steeringWheel = 0;         // угол поворота штурвала
    [SerializeField]
    private bool _GameWheel = false;        // управление от игрового руля

    [Header("Разные исходные данные:")]
    private float enginePower = 30000f;      // 40 л.с примерно 30 КВт
    private float maxV = 4.1f;               // 4.1 м/с = 8 узлов
    private float Ca = 0.97f;                // адмиралтейский коэффициент после перевода рассчетов в Си
    private float Lbody = 11.99f;            // длинна корпуса
    private float M0 = 8680f;                // водоизмещение = вес яхты в кг
    private float Jy = 35000f;               // момент инерции относительно вертикальной оси
    public float K11 = 0.3f;                // коеф. для расчета массы с учетом присоединенной Mzz = (1+K11)*M0
    public float K66 = 0.5f;                // коеф. для расчета массы и момента с учетом прис.  Jyy = (1+K66)*Jy; Mxx = (1+K66)*M0
    public float KFrudX = 0.5f;             // Подгонка, для реализма эффективности руля
    public float KBeta = 0.2f;              // Чтобы уменьшить влияние Beta, так как руль обдувается водой винта (3 стр 55)
    public float KrudVzxContraEnx = 0.7f;   // Соотношение влияния руля в потоке воды и руля в потоке винта
    public float KwindF = 0.5f;             // Для подстройки влияния ветра на силу
    public float KwindM = 1.0f;             // Для подстройки влияния ветра на момент

    // занос кормы
    public int DirectV = 1;                 // направление вращения, +1 - правый винт, -1 - левый;
    public float Kzanos = 3;                // подбором, влияет на силу заноса кормы
    public float Tzanos = 1.5f;             // подбором, влияет на время (обратно) действия силы заноса после увеличения мощности 

    // рассчитывается один раз
    private float _KresZ;                   // коэффициент перед V*V при рассчете силы сопротивления по Z
    private float _KresX;                   // коэффициент перед V*V при рассчете силы сопротивления по X
    private float _KresOmega1;              // коэффициент перед силой сопротивления воды вращению линейный по омега
    private float _KresOmega2;              // коэффициент перед силой сопротивления воды вращению квадратичный по омега
    private float _Mzz;                     // Масса с учетом присоединенной
    private float _Mxx;                     // Масса с учетом присоединенной
    private float _Jyy;                     // Момент с учетом присоединенной массы

    // рассчитывается на каждом шаге
    [Header("Вывод для контроля:")]
    public float Feng;                      // сила тяги, будет получатся из curEngineP делением на maxV (?)
    public float RuderValue;                // угол поворота пера руля
    public float FresZ;                     // сила сопротивления корпуса продольная
    public float FresX;                     // сила сопротивления корпуса поперечная

    public float FrudVzZ;                   // сила сопротивления руля от движения яхты относительно воды
    public float FrudVzX;                   // боковая сила на руле от движения яхты относительно воды
    public float MrudVzX;                   // Момент возникающий от силы FrudVzX на руле
    public float FrudEnZ;                   // доп. сила сопротивления (от винта) из-за поворота руля 
    public float FrudEnX;                   // боковая сила (от движения яхты) из-за поворота руля 
    public float MrudEnX;                   // Момент возникающий от силы FrudEnX на руле
    public float MrudResZ;                  // Момент на руле от сопротивления воды, когда есть Beta

    public float MresBody;                  // Момент сил сопротивления воды
    public float FengX;                     // боковая сила от винта, возникает при изменениях мощности
    public float MengX;                     // Момент от винта

    public float FwingX;                    // сила ветра по X
    public float FwingZ;                    // сила ветра по Z
    public float Mwing;                     // момент от ветра

    public float FropeX;                    // сила натяжения по X
    public float FropeZ;                    // сила натяжения по Z
    public float Mrope;                     // момент от натяжения 


    public float Vz = 0;                    // текущая продольная скорость
    public float Vx = 0;                    // боковая скорость
    public float OmegaY = 0;                // скорость поворота вокруг вертикальной оси
    public float Beta = 0;                  // угол между локальной осью OZ и скоростью

    // Ветер
    private Wind _wind;

    // канаты
    private Cleat[] _cleats;

    // Объекты сцены
    Transform _HelmWheel;                   // Штурвал
    Transform _ThrottleLever;               // Ручка газ-реверс
    Text _SpeedText;                        // Дисплей - скорость
    Text _RudderAngleText;                  // Дислей - положение руля
    Text _TrackAngleText;                   // Дисплей - курсовой угол
    //float _Mile = 1852.0f;                  // Морская миля = 1852 метра
    //float _Knot = 0.5144f;                  // Скорость 1 узел = 0.514... метр/сек.
    float _MeterSecToKnot = 1.944f;            // Скорость 1 метр/сек. = 1,943844492440605 узла

    private void Awake()
    {
        GameObject windField = GameObject.Find("WindField");
        _wind = windField.GetComponent<Wind>();

        _cleats = GameObject.FindObjectsOfType<Cleat>();
    }

    void Start()
    {
        // рассчет коэффициента перед силой сопротивления в ур. динамики (1-8)
        _KresZ = Mathf.Pow(M0, 2.0f / 3.0f) / Ca;
        _KresX = _KresZ * 20;                // примерно, подбором
        _KresOmega2 = _KresZ * 30;           // примерно, подбором
        _KresOmega1 = _KresZ;                // примерно, подбором
        // массы и момент инерции с учетом присоединенных масс
        _Mzz = (1 + K11) * M0;
        _Mxx = (1 + K66) * M0;
        _Jyy = (1 + K66) * Jy;

        // Объекты сцены
        _HelmWheel = GameObject.Find("HelmWheel").transform;                         // Штурвал
        _ThrottleLever = GameObject.Find("ThrottleLever").transform;                 // Ручка газ-реверс
        _SpeedText = GameObject.Find("SpeedText").GetComponent<Text>();              // Дисплей - скорость
        _RudderAngleText = GameObject.Find("RudderAngleText").GetComponent<Text>();  // Дислей - положение руля
        _TrackAngleText = GameObject.Find("TrackAngleText").GetComponent<Text>();    // Дисплей - курсовой угол

    }

    private void Update()
    {
        // получить управление с клавиатуры
        if (Input.GetKeyDown("up"))
            engineValue += 0.1f;
        else if (Input.GetKeyDown("down"))
            engineValue -= 0.1f;

        // получить управление от руля
        if (_GameWheel)
            steeringWheel = Mathf.Lerp(-540.0f, 540.0f, (Input.GetAxis("HorizontalJoy") + 1.0f) / 2.0f);
        else
        {
            if (Input.GetKey("right"))
                steeringWheel += 1.0f;
            else if (Input.GetKey("left"))
                steeringWheel -= 1.0f;
        }

        engineValue = Mathf.Clamp(engineValue, -1.0f, 1.0f);
        steeringWheel = Mathf.Clamp(steeringWheel, -540.0f, 540.0f);

        // Повернуть ручку газ-реверс на 3d модели
        Vector3 myVect = _ThrottleLever.localEulerAngles;
        myVect.x = Mathf.Lerp(-50, 50, (engineValue + 1) / 2.0f);
        _ThrottleLever.localEulerAngles = myVect;


        // Повернуть штурвал на 3d модели
        myVect = _HelmWheel.localEulerAngles;
        myVect.z = -steeringWheel;
        //myVect.z = - Mathf.Lerp(-540, 540, (steeringWheel + 35) / 70.0f);  
        _HelmWheel.localEulerAngles = myVect;

        // Вывести данные на дисплеи на 3d модели
        _SpeedText.text = (Vz * _MeterSecToKnot).ToString("F2", CultureInfo.InvariantCulture);
        _RudderAngleText.text = RuderValue.ToString("F2", CultureInfo.InvariantCulture);
        _TrackAngleText.text = NormalizeAngle(transform.localEulerAngles.y).ToString("F0", CultureInfo.InvariantCulture);

    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // поворот пера руля
        RuderValue = -steeringWheel * 35 / 540;
        // сила тяги
        float FengOld = Feng; // для анализа, нужен ли занос кормы от работы винта
        Feng = enginePower * engineValue / maxV;

        // сила сопротивления корпуса
        FresZ = -Mathf.Sign(Vz) * _KresZ * Vz * Vz;
        FresX = -Mathf.Sign(Vx) * _KresX * Vx * Vx;

        // силы и момент на руле от движения яхты
        FrudVzZ = -Mathf.Sign(Vz) * FruderZ(RuderValue - Beta, Vz);
        FrudVzX = Mathf.Sign(RuderValue - Beta) * Mathf.Sign(Vz) * FruderX(RuderValue - Beta, Vz);
        FrudVzX *= KrudVzxContraEnx;    // доля влияния руля в потоке воды
        MrudVzX = -FrudVzX * Lbody / 2;
        //print("RuderValue = " + RuderValue + "   Beta = " + Beta + "   Итого = " + (RuderValue - Beta));

        // силы и момент на руле от работы винта - возникают только при кручении винта вперед
        if (Feng > 0)
        {
            float VeffRud = Mathf.Sqrt(Feng / 440);
            FrudEnZ = -FruderZ(RuderValue, VeffRud);
            FrudEnX = Mathf.Sign(RuderValue) * FruderX(RuderValue, VeffRud);
            FrudVzX *= (1 - KrudVzxContraEnx);    // доля влияния руля в потоке винта
            MrudEnX = -FrudEnX * Lbody / 2;
        }
        else
        {
            FrudEnZ = FrudEnX = MrudEnX = 0.0f;

        }
        // Момент на руле из-за сопротивления руля воде при наличии угла Beta
        if (Vz > 0)
        {
            // сделал только при движении вперед, чтобы не попадать в штопор на заднем ходу
            MrudResZ = -(FrudVzZ + FrudEnZ) * Mathf.Sin(Beta * Mathf.PI / 180) * Lbody / 2;
        }
        else
        {
            MrudResZ = 0;
        }

        // Момент - сопротивление вращательному движению. Не по (1), а из физических соображений
        //MresBody = -Mathf.Sign(OmegaY) * _KresOmega2 * (OmegaY * Lbody) * (OmegaY * Lbody) / 8;
        MresBody = -Mathf.Sign(OmegaY) * _KresOmega2 * (OmegaY * Lbody) * (OmegaY * Lbody) / 8 - _KresOmega1 * (OmegaY * Lbody) / 2;
        // сила и момент от увеличения мощности двигателя
        float impactX = detectEngineImpact(FengOld, Feng);
        float dFengX = dt * (Kzanos * impactX - Tzanos * FengX);    // спадающая экспонента
        FengX += dFengX;
        MengX = -FengX * Lbody / 2;

        // Влияние ветра:
        // в глобальной системе
        float wZ = _wind.WindDir[0].value * Mathf.Cos(_wind.WindDir[0].angle * Mathf.PI / 180);
        float wX = _wind.WindDir[0].value * Mathf.Sin(_wind.WindDir[0].angle * Mathf.PI / 180);
        //print("*************************");
        //print("wZ = " + wZ + "   wX = " + wX);
        Vector3 wGlobal = new Vector3(wX, 0, wZ);
        // в локальной системе
        Vector3 wLocal = transform.InverseTransformVector(wGlobal);
        wLocal.x -= Vx;
        wLocal.z -= Vz;
        float wAngle = Vector3.Angle( Vector3.forward, wLocal ) * Mathf.Sign(wLocal.x);  // направление отсчитываем от носа
        wAngle = NormalizeAngle(wAngle);
        //print("Локально: " + wLocal + "   wAngle = " + wAngle);
        float wValue = wLocal.magnitude;
        // получаем величину силы и момент от ветра
        float fWind = WindForce(wAngle, wValue);           // возвращается абс. величина силы!
        //print("fWind = " + fWind);
        Vector3 FwingVec = Vector3.Normalize(wLocal) * fWind;
        FwingX = FwingVec.x;
        FwingZ = FwingVec.z;
        Mwing = WindMoment(wAngle, wValue);
        //print("FwingZ = " + FwingZ + "   FwingX = " + FwingX + "   Mwing = " + Mwing);

        // Натяжение канатов
        FropeX = 0;
        FropeZ = 0;                    
        Mrope = 0;                     
        for(int i=0; i< _cleats.Length; i++)
        {
            Vector3 localF = transform.InverseTransformDirection( _cleats[i].getForce() );
            FropeX += localF.x;
            FropeZ += localF.z;
            Mrope += localF.x * (_cleats[i].transform.localPosition.z);
            Mrope += -localF.z * (_cleats[i].transform.localPosition.x);
        }

        // Численное интегрирование:
        // Интегрируем dVz/dt - продольная скорость по Z
        float rotToVz = _Mxx * OmegaY * Vx;
        float dVz = dt * (Feng + FresZ + FrudVzZ + FrudEnZ + FwingZ + FropeZ + rotToVz) / _Mzz;

        // Интегрируем dVx/dt - боковая скорость по X
        float rotToVx = -_Mzz * OmegaY * Vz;
        float dVx = dt * (FrudVzX + FrudEnX + FresX + FengX + FwingX + FropeX + rotToVx) / _Mxx;
        //print("FrudX = " + FrudX + "   FresX = " + FresX + "   rotToVy = " + rotToVx + "   dVx = "+ dVx + "   Vx = "+Vx);

        // Интегрируем dOmegaY/dt - момент вокруг вертикальной оси Y
        float vToRot = (_Mzz - _Mxx) * Vx * Vz;
        float dOmegaY = dt * (MrudVzX + MrudEnX + MresBody + MengX + MrudResZ + Mwing + Mrope + vToRot) / _Jyy;

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


        // определение угла Beta между продольной осью и скоростью
        if (Vz == 0.0f && Vx == 0.0f)
        {
            Beta = 0;
        }
        else if (Vz == 0.0f && Vx != 0.0f)
        {
            Beta = 90 * Mathf.Sign(Vx);
        }
        else
        {
            Beta = Mathf.Atan(Vx / Vz) * 180 / Mathf.PI;
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
        Fv3 = 12.3f + ang * (-0.311f + ang * (0.103f + ang * 0.011f));

        return Fv3 / VV * V * V;
    }

    // сила сопротивления руля по оси X, порождает боковую скорость и момент вращения
    private float FruderX(float ang, float V)
    {
        float VV = 2.37f;           // если скорость 1,54 то V*V = 2,37
        float Fv3;                  // сила при скорости 3 узла 

        ang = Mathf.Abs(ang);
        Fv3 = ang * (59.88f + ang * (-2.57f + ang * 0.029f));

        return Fv3 / VV * V * V * KFrudX; // KFrudX - подгонка, чтобы уменьшить эффективность руля
    }

    // боковая сила приложеная к корме из-за увеличения мощности двигателя
    // TODO! не все ситуации рассмотрены!
    private float detectEngineImpact(float FengOld, float Feng)
    {
        float impact = 0;
        if (FengOld >= 0 && FengOld < Feng)     // мощность увеличили
        {
            if (Vz >= 0)                        // стоим или плывем вперед
            {
                impact = Kzanos * (Feng - FengOld);
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
        return impact * DirectV;
    }

    // Приведем любой угол от к (-180/+180)
    float NormalizeAngle(float myAngle)
    {
        while (myAngle > 180.0f)
        {
            myAngle -= 360.0f;
        }
        while (myAngle < -180.0f)
        {
            myAngle += 360.0f;
        }
        return myAngle;
    }

    // Получение силы в зависимости от угла и скорости ветра
    private float  WindForce( float alfa, float v)
    {
        float x = Mathf.Abs(alfa);

        //y = 9E-08x4 - 3E-05x3 + 0,002x2 + 0,008x + 1,042
        //float wingf = 1.042f + x * (0.008f + x * (0.002f + x * (-0.00003f + x * 0.00000009f)));
        //return wingf * v * v * KwindF * Mathf.Sign(alfa);

        // y = 7E-08x4 - 3E-05x3 + 0,002x2 - 0,007x   + для смещения кривой вверх 0,5 
        //float wingf = x * (x * (x * (x * 0.00000007f - 0.00003f) + 0.002f) - 0.007f)+0.5f;
        //wingf = 10;
        //print("WindForce: x = " + x);
        float wingf = Mathf.Sin(Mathf.PI * x / 180) * 4 + 0.5f;
        return wingf * v * v * KwindF;
    }

    // Получение момента в зависимости от угла и скорости ветра
    private float WindMoment(float alfa, float v)
    {
        float x = Mathf.Abs(alfa);
        //y = 1E-09x5 - 5E-07x4 + 7E-05x3 - 0,005x2 + 0,186x - 0,152
        //float wingm = -0.152f + x * (0.186f + x * (-0.005f + x * (0.00007f + x * (-0.0000005f + x * 0.000000001f))));
        //return wingm * v * v * KwindM * Mathf.Sign(alfa); 

        // y = 7E-08x4 - 3E-05x3 + 0,002x2 - 0,007x 
        //float wingm = x * (x * (x * (x * 0.00000007f - 0.00003f) + 0.002f) - 0.007f);
        float wingm = Mathf.Sin(Mathf.PI * x / 180) * 4;
        return wingm * v * v * KwindM * Mathf.Sign(alfa);
    }


}
