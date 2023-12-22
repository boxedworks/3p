using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameEntity : Mirror.NetworkBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public struct DamageData{
      public float Damage;

      public GameEntity DamageSource;
    }
    public virtual void TakeDamage(DamageData damageData){

    }

}
