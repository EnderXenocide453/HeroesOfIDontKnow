using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterController : MonoBehaviour
{
    public Vector3Int tilePos;
    public Unit unit;

    //Какому игроку принадлежит
    public int faction = 0;

    public delegate void CharacterEventHandler();
    public event CharacterEventHandler onMoveEnd;

    private List<Vector3> _way;

    private void Start()
    {
        unit.onDamageDone += (object obj) => { Debug.Log(((int, int))obj); };
    }

    public void SetWay(List<Vector3> way)
    {
        _way = way;
        StartCoroutine(Move());
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

    private IEnumerator Move()
    {
        while (_way.Count > 0) {
            transform.position = Vector3.MoveTowards(transform.position, _way[0], unit.speed * Time.deltaTime);
            if (transform.position == _way[0])
                _way.RemoveAt(0);

            yield return new WaitForEndOfFrame();
        }
        
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

    # region Основные характеристики
    public int count = 1;
    public int distance;
    public int minDmg;
    public int maxDmg;
    public int health;
    public float initiative;
    public float defense = 0;

    private int _curHealth;
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
    }

    public void GetDamage(int dmg)
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

    public void GetHeal(int amount, bool resurrect = false)
    {
        onHealStart?.Invoke(amount);
        _curHealth += amount;
        int resCount = 0;

        if (resurrect) {
            resCount = _curHealth / health - 1;

            count += resCount;
        }
        
        _curHealth = Mathf.Clamp(_curHealth, 0, health);
        onHealDone?.Invoke((amount, resCount));
    }

    private void Death()
    {
        onDeath?.Invoke(ID);
    }
}

public class Knight : Unit
{
    public Knight(int count = 1)
    {
        distance = 5;
        health = 20;
        minDmg = 6;
        maxDmg = 10;
        initiative = 4;
        this.count = count;

        defense = 0.99f;

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
    Knight
}