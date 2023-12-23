using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class GameEntity : NetworkBehaviour
{

  //
  public bool _IsPlayer { get { return this is PlayerController; } }
  public bool _IsEnemy { get { return this is EnemyScript; } }

  //
  public override void OnStartClient()
  {
    s_GameEntitiesMapped.Add(netId, this);
    Debug.Log("Adding " + netId);
  }

  // Damage methods
  [System.Serializable]
  public struct DamageData
  {
    public float Damage;

    public uint DamageSourceNetId;
    public GameEntity DamageSource { get { return s_GameEntitiesMapped[DamageSourceNetId]; } }
  }
  public void TakeDamage(DamageData damageData)
  {
    HandleDamage(damageData);
    CmdTakeDamage(damageData);
  }
  public virtual void HandleDamage(DamageData damageData)
  {

  }
  [Command(requiresAuthority = false)]
  void CmdTakeDamage(DamageData damageData)
  {
    foreach (var player in PlayerController.s_Players)
      if (damageData.DamageSourceNetId != player.netId)
        TargetTakeDamage(player.connectionToClient, damageData);
  }
  [TargetRpc]
  void TargetTakeDamage(NetworkConnectionToClient target, DamageData damageData)
  {
    HandleDamage(damageData);
  }

  // Static members
  public static Dictionary<uint, GameEntity> s_GameEntitiesMapped;
  public static void Init()
  {
    s_GameEntitiesMapped = new();
  }
}
