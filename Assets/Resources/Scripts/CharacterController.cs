using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CharacterController : MonoBehaviour
{
    public Vector3Int tilePos;
    public Unit unit;

    //Какому игроку принадлежит
    public int faction = 0;

    public delegate void CharacterEventHandler();
    public event CharacterEventHandler onMoveEnd;

    //Счетчик отряда
    public TMP_Text countField;
    //Поле с именем
    public TMP_Text nameField;

    private void Start()
    {
        countField.text = unit.count.ToString();
        nameField.text = unit.name;

        GetComponent<SpriteRenderer>().color = new Color(1 - faction * 0.2f, 1, 1 - 0.2f * (1 - faction));

        unit.onDamageDone += (object obj) => { countField.text = unit.count.ToString(); };
        unit.onHealDone += (object obj) => { countField.text = unit.count.ToString(); };
    }

    public void SetWay(List<Vector3> way)
    {
        StartCoroutine(Move(way));
    }

    public void Attack()
    {
        //Проигрываем анимацию атаки
    }

    public void Death()
    {
        Destroy(gameObject);
        //Анимация смерти
    }

    private IEnumerator Move(List<Vector3> way)
    {
        while (way.Count > 0) {
            transform.position = Vector3.MoveTowards(transform.position, way[0], unit.speed * Time.deltaTime);
            if (transform.position == way[0])
                way.RemoveAt(0);

            yield return new WaitForEndOfFrame();
        }

        Debug.Log(onMoveEnd);

        onMoveEnd?.Invoke();
    }
}

public abstract class Unit
{
    //Скорость перемещения по карте
    public float speed = 2f;

    //Дальняя ли атака
    public bool isRange = false;

    //ID
    public int ID;

    //Активная способность
    public (AbilityType type, Vector3Int[] area, int amount) abilityData;

    # region Основные характеристики
    public string name;

    public int count = 1;
    public int distance;
    public int minMeleeDmg;
    public int maxMeleeDmg;
    public int minRangeDmg;
    public int maxRangeDmg;
    public int health;
    public float initiative;
    public float defense = 0;

    public int maxRepeat = 0;
    public float repeatChance = 0;

    private int _curHealth;
    private int _maxCount;
    #endregion

    #region События
    public delegate void UnitEventHandler(object target);
    public event UnitEventHandler onDamageStart;
    public event UnitEventHandler onDamageDone;
    public event UnitEventHandler onHealStart;
    public event UnitEventHandler onHealDone;
    public event UnitEventHandler onDeath;
    public event UnitEventHandler onTurnEnds;
    #endregion

    protected void InitStats()
    {
        _curHealth = health;
        _maxCount = count;
    }

    public void DealDamage(int dmg)
    {
        onDamageStart?.Invoke(dmg);

        //Броня не может повернуть урон вспять или сделать юнит уязвимым более чем в 2 раза
        dmg *= Mathf.RoundToInt(1 - Mathf.Clamp(defense, -1, 1));

        _curHealth -= dmg;
        int deadCount = 0;

        if (_curHealth <= 0) {
            deadCount = Mathf.Abs(_curHealth / health) + 1;
            count -= deadCount;

            if (count < 1) Death();

            _curHealth = health + _curHealth % health;
        }

        onDamageDone?.Invoke((dmg, deadCount));
    }

    public int GetRandomDamage(bool range = false)
    {
        int minDmg = minMeleeDmg, maxDmg = maxMeleeDmg;

        if (range) (minDmg, maxDmg) = (minRangeDmg, maxRangeDmg);

        return (int)Mathf.Lerp(minDmg * count, maxDmg * count, Random.Range(0.0f, 1.0f));
    }

    public void GetHeal(int amount, bool resurrect = false)
    {
        onHealStart?.Invoke(amount);
        _curHealth += amount;
        int resCount = 0;

        if (resurrect) {
            resCount = _curHealth / health - 1;

            count += resCount;

            if (count > _maxCount) {
                count = _maxCount;
                _curHealth = health;
            }
        }
        
        _curHealth = Mathf.Clamp(_curHealth, 1, health);
        onHealDone?.Invoke((amount, resCount));
    }

    public bool EndTurn(int repeatCount = 0)
    {
        if (repeatCount >= maxRepeat)
            return true;

        if (Random.Range(0.0f, 1.0f) < repeatChance)
            return false;

        return true;
    }

    private void Death()
    {
        onDeath?.Invoke(ID);
    }
}

public class Archer : Unit
{
    public Archer(int count = 1)
    {
        name = "Лучник";
        isRange = true;

        distance = 6;
        health = 20;

        minRangeDmg = 6;
        maxRangeDmg = 15;

        minMeleeDmg = 1;
        maxMeleeDmg = 6;
        initiative = 6;
        this.count = count;

        InitStats();

        abilityData = (AbilityType.Attack,  
            new Vector3Int[] //Список плиток в области атаки
            {
                new Vector3Int(0, 0, 0),
                new Vector3Int(0, -1, 0),
                new Vector3Int(0, -2, 0),
                new Vector3Int(0, -3, 0),
                new Vector3Int(0, -4, 0),
                new Vector3Int(1, 0, 0),
                new Vector3Int(1, -1, 0),
                new Vector3Int(1, -2, 0),
                new Vector3Int(1, -3, 0),
                new Vector3Int(1, -4, 0),
                new Vector3Int(-1, -1, 0),
                new Vector3Int(-1, -3, 0)
            },
            GetRandomDamage() / 2 //Урон от способности
        );
    }
}

public class Knight : Unit
{
    public Knight(int count = 1)
    {
        name = "Рыцарь";

        distance = 5;
        health = 20;
        minMeleeDmg = 6;
        maxMeleeDmg = 10;
        initiative = 4;
        this.count = count;

        defense = 0.99f;

        maxRepeat = 1;
        repeatChance = 0.45f;

        InitStats();

        onDamageDone += DecreaseDefense;
    }

    //Снижение защиты
    private void DecreaseDefense(object obj)
    {
        if (defense > 0)
            defense -= 0.33f;
    }
}

public enum UnitName
{
    Knight,
    Archer,
    Priest,
    Skeleton,
    WeakSkeleton,
    Zombie
}