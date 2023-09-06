using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterController : MonoBehaviour
{
    public int ID;

    public Unit unit;

    private List<Vector3> _way;

    private void Start()
    {
        _way = new List<Vector3>();
        unit = new Knight();
    }

    private void Update()
    {
        Move();
    }

    public void SetWay(List<Vector3> way)
    {
        _way = way;
    }

    private void Move()
    {
        if (_way.Count == 0)
            return;

        transform.position = Vector3.Lerp(transform.position, _way[0], unit.speed);
        if (transform.position == _way[0])
            _way.RemoveAt(0);
    }
}

public abstract class Unit
{
    //—корость перемещени€ по карте
    public float speed = 0.05f;

    # region ќсновные характеристики
    public int count = 1;
    public int distance;
    public int minDmg;
    public int maxDmg;
    public int health;
    public float initiative;
    public float defense = 0;

    private int _curHealth;
    #endregion

    #region —обыти€
    public delegate void UnitEventHandler(object target = null);
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

        //Ѕрон€ не может повернуть урон всп€ть или сделать юнит у€звимым более чем в 2 раза
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
        onDeath.Invoke();
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
    }
}

public enum UnitName
{
    Knight
}