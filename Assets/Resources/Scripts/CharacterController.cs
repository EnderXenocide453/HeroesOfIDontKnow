using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterController : MonoBehaviour
{
    public int ID;

    public Unit unit;
    public int count;

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
    public int distance = 4;
    public float speed = 0.1f;
    public float initiative = 1;
}

public class Knight : Unit
{

}