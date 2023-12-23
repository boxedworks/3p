using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;

public class GameController : MonoBehaviour
{
  // Start is called before the first frame update
  void Start()
  {

    GameEntity.Init();
    PlayerController.Init();
    EnemyScript.Init();

#if UNITY_EDITOR
    NetworkManager.singleton.StartHost();

    EnemyScript.SpawnEnemy(new EnemyScript.EnemySpawnData()
    {
      EnemyType = EnemyScript.EnemyType.FLY_SIMPLE,
      Position = new Vector3(Random.Range(-30f, 30f), 5f, Random.Range(-30f, 30f))
    });
    EnemyScript.SpawnEnemy(new EnemyScript.EnemySpawnData()
    {
      EnemyType = EnemyScript.EnemyType.FLY_SIMPLE,
      Position = new Vector3(Random.Range(-30f, 30f), 5f, Random.Range(-30f, 30f))
    });
    EnemyScript.SpawnEnemy(new EnemyScript.EnemySpawnData()
    {
      EnemyType = EnemyScript.EnemyType.CHASE_SIMPLE,
      Position = new Vector3(Random.Range(-30f, 30f), 5f, Random.Range(-30f, 30f))
    });
#else
NetworkManager.singleton.StartClient();
#endif

  }

  // Update is called once per frame
  void Update()
  {

  }

  //

}
