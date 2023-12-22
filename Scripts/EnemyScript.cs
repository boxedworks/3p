using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using Mirror;

public class EnemyScript : GameEntity
{

  //
  public EnemyType _EnemyType { get { return _enemyType; } }
  EnemyType _enemyType;
  public enum EnemyType
  {
    NONE,

    FLY_SIMPLE,

    CHASE_SIMPLE,
    CHASE_LARGE,
  }

  //
  Collider _collider;
  PlayerController _targetClosest;

  Vector3 _targetPosition;
  GameObject[] _gameObjects;
  Material[] _materials;

  float _movementSpeed;
  float _directionSwitch;

  // Client init
  bool _initted;
  public override void OnStartClient()
  {
    Debug.Log($"startclient {_initted}");
    if (!_initted)
    {
      CmdSyncEnemyData(connectionToClient);
    }
  }
  [Command(requiresAuthority = false)]
  void CmdSyncEnemyData(NetworkConnectionToClient connectionToClient)
  {
    TargetSyncEnemyData(connectionToClient, _enemyType);
  }
  [TargetRpc]
  void TargetSyncEnemyData(NetworkConnectionToClient target, EnemyType enemyType)
  {
    Init(new EnemySpawnData()
    {
      EnemyType = enemyType
    });
  }

  // Start is called before the first frame update
  public void Init(EnemySpawnData enemySpawnData)
  {
    _initted = true;

    _enemyType = enemySpawnData.EnemyType;

    var enemyModel = GameObject.Instantiate(GameObject.Find($"Enemy{((int)_enemyType) - 1}"));
    enemyModel.transform.parent = transform;
    enemyModel.transform.localPosition = Vector3.zero;
    enemyModel.transform.localRotation = Quaternion.identity;

    _collider = transform.GetChild(0).GetChild(1).GetComponent<Collider>();
    transform.position = enemySpawnData.Position;
    Debug.Log($"{netId} .. initting ... {_collider.gameObject.GetInstanceID()}");

    s_Enemies.Add(this);
    s_EnemiesMapped.Add(_collider.gameObject.GetInstanceID(), this);

    _physicsData = new();
    _directionSwitch = Random.Range(1f, 2f) * (Random.Range(0, 2) == 0 ? -1f : 1f);

    var health = 100f;
    switch (_enemyType)
    {

      case EnemyType.FLY_SIMPLE:

        _targetPosition = new Vector3(Random.Range(-15f, 15f), Random.Range(5f, 9f), Random.Range(-15f, 15f));
        _attackTimeNext = Time.time + Random.Range(2f, 4f);

        // Gather laser
        _gameObjects = new GameObject[]{
          transform.GetChild(0).GetChild(2).gameObject
        };

        break;

      case EnemyType.CHASE_SIMPLE:

        _movementSpeed = Random.Range(7f, 8.5f);
        _targetPosition = new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
        _attackTimeNext = Time.time + Random.Range(2f, 4f);

        // Gather materials
        var renderer = transform.GetChild(0).GetChild(2).GetComponent<MeshRenderer>();
        _materials = new Material[]{
          new Material(renderer.sharedMaterials[0])
        };
        renderer.sharedMaterials = _materials;

        break;

      case EnemyType.CHASE_LARGE:

        health = 200f;

        _movementSpeed = Random.Range(4f, 5f);
        _targetPosition = new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
        _attackTimeNext = Time.time + Random.Range(2f, 4f);

        // Gather materials
        renderer = transform.GetChild(0).GetChild(2).GetComponent<MeshRenderer>();
        _materials = new Material[]{
          new Material(renderer.sharedMaterials[0])
        };
        renderer.sharedMaterials = _materials;

        break;

    }
    _healthData = new(health);
  }

  //
  public override void TakeDamage(DamageData damageData)
  {
    _healthData._Health = Mathf.Clamp(_healthData._Health - damageData.Damage, 0, _healthData._HealthMax);
    if (_healthData._Health <= 0f)
    {
      for (var i = 0; i < Random.Range(1, 3); i++)
        SpawnEnemy(new EnemySpawnData()
        {
          EnemyType = (EnemyType)Random.Range(1, 4),
          Position = new Vector3(Random.Range(-30f, 30f), 5f, Random.Range(-30f, 30f))
        });
      OnDestroy();
      return;
    }

    if (!_healthData._Slider.gameObject.activeSelf)
      _healthData.ToggleVisibility();
    _healthData.UpdateUI();
  }

  //
  void OnDestroy()
  {
    //
    s_Enemies.Remove(this);
    s_EnemiesMapped.Remove(_collider.gameObject.GetInstanceID());

    //
    if (_healthData._Slider != null)
      GameObject.Destroy(_healthData._Slider.gameObject);
    if (_materials != null)
    {
      for (var i = _materials.Length - 1; i > -1; i--)
        GameObject.Destroy(_materials[i]);
      _materials = null;
    }
    GameObject.Destroy(gameObject);
  }

  //
  PhysicsData _physicsData;
  struct PhysicsData
  {

    public Vector3 Velocity;

  }

  //
  HealthData _healthData;
  class HealthData
  {
    public float _Health, _HealthMax;

    public UnityEngine.UI.Slider _Slider;

    public bool _Visible;

    public HealthData(float healthMax)
    {
      _HealthMax = _Health = healthMax;

      var sliderBase = GameObject.Find("EnemyHealths").transform.GetChild(0);
      _Slider = GameObject.Instantiate(sliderBase).GetComponent<UnityEngine.UI.Slider>();
      _Slider.transform.parent = sliderBase.transform.parent;
    }

    public void UpdateUI()
    {
      _Slider.value = _Health / _HealthMax;
    }

    public void ToggleVisibility()
    {
      _Visible = !_Visible;
      _Slider.gameObject.SetActive(_Visible);
    }
  }

  // Update is called once per frame
  float _attackTime, _attackTimeNext, _attackFinishTime, _lastDistanceToTarget;
  Vector3 _attackPosition;
  void Update()
  {

    CheckAttack();
    if (!isOwned) return;

    switch (_enemyType)
    {

      case EnemyType.FLY_SIMPLE:

        if (_targetClosest == null) return;

        // Movement
        var desiredPosition = _targetClosest.transform.position + _targetPosition;
        var dirToTarget = (desiredPosition - transform.position).normalized;
        _physicsData.Velocity += (dirToTarget - _physicsData.Velocity) * Time.deltaTime * 4f;
        transform.position += _physicsData.Velocity * Time.deltaTime * 3f;

        transform.LookAt(_targetClosest.transform.position);

        break;

      case EnemyType.CHASE_SIMPLE:

        if (_targetClosest == null) return;

        // Movement
        var dirToPlayer = (_targetClosest.transform.position - transform.position).normalized;
        desiredPosition = _targetClosest.transform.position + -dirToPlayer * 1.5f + _targetPosition;
        dirToTarget = ((desiredPosition - transform.position).normalized + (_lastDistanceToTarget > 4f ? _directionSwitch * transform.right : Vector3.zero)).normalized;
        _physicsData.Velocity += (dirToTarget - _physicsData.Velocity) * Time.deltaTime * 3f;
        transform.position += _physicsData.Velocity * Time.deltaTime * _movementSpeed;

        // Check attack
        if (Time.time - _attackTimeNext > 0f)
        {

          // Distance to player
          var distanceToPlayer = _lastDistanceToTarget = Vector3.Distance(_targetClosest.transform.position, transform.position);
          if (distanceToPlayer < 8f)
          {
            if (distanceToPlayer < 6f)
            {
              CmdAttack();
            }

          }
        }

        // Attack
        var aoe = transform.GetChild(0).GetChild(2).gameObject;
        var attackSpeed = 0.5f;
        if (Time.time - _attackTime < attackSpeed)
        {

          if (!aoe.activeSelf)
          {
            _materials[0].SetFloat("_IntersectionDepth", 0.5f);
            aoe.SetActive(true);
          }
          else
            _materials[0].SetFloat("_IntersectionDepth", 0.5f + ((Time.time - _attackTime) / attackSpeed) * 1.2f);
        }

        // Damage
        else
        {
          if (_attackTime != -1f)
          {
            _attackTime = -1f;
            _attackFinishTime = Time.time;

            var colliders = Physics.OverlapBox(aoe.transform.position, aoe.transform.localScale * 0.5f, Quaternion.identity, LayerMask.GetMask("Player"));
            foreach (var collider in colliders)
            {
              var playerGot = PlayerController.GetPlayer(collider);
              if (playerGot != null)
              {
                playerGot.TakeDamage(new DamageData()
                {
                  Damage = 25f,

                  DamageSource = this
                });
              }
            }
          }

          //
          var interectionDepth = (1f - (Time.time - _attackFinishTime) / 0.13f) * 2f;
          if (interectionDepth > 0f)
          {
            _materials[0].SetFloat("_IntersectionDepth", interectionDepth);
          }
          else
          {
            if (aoe.activeSelf)
              aoe.SetActive(false);
            transform.LookAt(transform.position + dirToPlayer);
          }
        }

        break;

      case EnemyType.CHASE_LARGE:

        if (_targetClosest == null) return;

        // Movement
        dirToPlayer = (_targetClosest.transform.position - transform.position).normalized;
        desiredPosition = _targetClosest.transform.position + -dirToPlayer * 1.5f + _targetPosition;
        dirToTarget = ((desiredPosition - transform.position).normalized + (_lastDistanceToTarget > 4f ? _directionSwitch * transform.right : Vector3.zero)).normalized;
        _physicsData.Velocity += (dirToTarget - _physicsData.Velocity) * Time.deltaTime * 3f;
        transform.position += _physicsData.Velocity * Time.deltaTime * _movementSpeed;

        // Check attack
        if (Time.time - _attackTimeNext > 0f)
        {

          // Distance to player
          var distanceToPlayer = _lastDistanceToTarget = Vector3.Distance(_targetClosest.transform.position, transform.position);
          if (distanceToPlayer < 12f)
          {
            if (distanceToPlayer < 9f)
            {
              _attackTime = Time.time;
              _attackTimeNext = Time.time + Random.Range(5f, 9f);
            }

          }
        }

        // Attack
        aoe = transform.GetChild(0).GetChild(2).gameObject;
        attackSpeed = 1f;
        if (Time.time - _attackTime < attackSpeed)
        {

          if (!aoe.activeSelf)
          {
            _materials[0].SetFloat("_IntersectionDepth", 0.5f);
            aoe.SetActive(true);
          }
          else
            _materials[0].SetFloat("_IntersectionDepth", 0.5f + ((Time.time - _attackTime) / attackSpeed) * 1.2f);
        }

        // Damage
        else
        {
          if (_attackTime != -1f)
          {
            _attackTime = -1f;
            _attackFinishTime = Time.time;

            var colliders = Physics.OverlapSphere(aoe.transform.position, aoe.transform.localScale.x * 0.5f, LayerMask.GetMask("Player"));
            foreach (var collider in colliders)
            {
              var playerGot = PlayerController.GetPlayer(collider);
              if (playerGot != null)
              {
                playerGot.TakeDamage(new DamageData()
                {
                  Damage = 25f,

                  DamageSource = this
                });
              }
            }
          }

          //
          var interectionDepth = (1f - (Time.time - _attackFinishTime) / 0.13f) * 2f;
          if (interectionDepth > 0f)
          {
            _materials[0].SetFloat("_IntersectionDepth", interectionDepth);
          }
          else
          {
            if (aoe.activeSelf)
              aoe.SetActive(false);
            transform.LookAt(transform.position + dirToPlayer);
          }
        }

        break;

    }

    //
    if (_healthData != null)
    {


      _healthData._Slider.transform.position = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);

    }

  }

  //
  void CheckAttack()
  {
    switch (_EnemyType)
    {
      case EnemyType.FLY_SIMPLE:

        // Attack
        var dirToPlayer = (_targetClosest.transform.position - transform.position).normalized;
        if (Time.time - _attackTimeNext > 0f)
        {
          _attackTime = Time.time;
          _attackTimeNext = Time.time + Random.Range(5f, 9f);
          _targetPosition = new Vector3(Random.Range(-15f, 15f), Random.Range(5f, 9f), Random.Range(-15f, 15f));

          var raycasthit = new RaycastHit();
          if (Physics.SphereCast(new Ray(transform.position, dirToPlayer), 0.1f, out raycasthit, 100f, LayerMask.GetMask("Default")))
          {
            _attackPosition = raycasthit.point;
          }
          else
          {
            _attackPosition = transform.position + dirToPlayer * 100f;
          }
        }

        var laser = _gameObjects[0];
        var attackSpeed = 0.5f;
        var dirToTarget = _attackPosition - transform.position;
        if (Time.time - _attackTime < attackSpeed)
        {
          var attackNormalized = (Time.time - _attackTime) / attackSpeed;

          var distanceToTarget = dirToTarget.magnitude;
          var laserWidth = 0.07f;
          laser.transform.localScale = new Vector3(laserWidth * attackNormalized, distanceToTarget * 0.5f, laserWidth * attackNormalized);
          laser.transform.position = transform.position + dirToTarget.normalized * distanceToTarget * 0.5f;
          laser.transform.LookAt(_attackPosition);
          laser.transform.Rotate(new Vector3(90f, 0f, 0f));

          if (!laser.activeSelf)
            laser.SetActive(true);
        }

        // Shoot
        else
        {
          if (laser.activeSelf)
          {
            laser.SetActive(false);

            //
            if (isOwned)
            {
              var raycasthit = new RaycastHit();
              if (Physics.SphereCast(new Ray(transform.position, dirToTarget), 0.1f, out raycasthit, 100f, LayerMask.GetMask("Default", "Player")))
              {
                var playerGot = PlayerController.GetPlayer(raycasthit.collider);
                if (playerGot != null)
                {
                  playerGot.TakeDamage(new DamageData()
                  {
                    Damage = 25f,

                    DamageSource = this
                  });
                }
              }
            }
          }
        }

        break;
    }
  }

  //
  [ClientRpc]
  void RpcSetTarget(uint playerNetId)
  {
    _targetClosest = PlayerController.GetPlayer(playerNetId);
  }

  //
  [Command(requiresAuthority = false)]
  void CmdAttack()
  {
    RpcAttack();
  }
  [ClientRpc]
  void RpcAttack()
  {
    _attackTime = Time.time;
    _attackTimeNext = Time.time + Random.Range(5f, 9f);
  }

  //
  public static List<EnemyScript> s_Enemies;
  public static Dictionary<int, EnemyScript> s_EnemiesMapped;

  public static void Init()
  {
    s_Enemies = new();
    s_EnemiesMapped = new();
  }

  // Update enemies incrementally for systems
  static int s_enemyIndex;
  [Server]
  public static void UpdateIncr()
  {

    var numEnemiesUpdate = Mathf.Clamp(3, 0, s_Enemies.Count);
    while (numEnemiesUpdate-- > 0)
    {

      var enemy = s_Enemies[s_enemyIndex++];
      s_enemyIndex %= s_Enemies.Count;

      // Set enemy closest target
      var players = PlayerController.s_Players;
      var closestPlayerIndex = -1;
      if (players.Count == 1)
        closestPlayerIndex = 0;
      else
      {
        var closestPlayerDist = 1000f;
        for (var i = 0; i < players.Count; i++)
        {

          var player = players[i];
          var distanceToPlayer = Vector3.Distance(enemy.transform.position, player.transform.position);
          if (distanceToPlayer < closestPlayerDist)
          {
            closestPlayerDist = distanceToPlayer;
            closestPlayerIndex = i;
          }

        }
      }

      if (closestPlayerIndex > -1)
      {
        var playerClosest = players[closestPlayerIndex];
        if ((enemy._targetClosest?.netId ?? 0) != playerClosest.netId)
        {
          enemy.RpcSetTarget(playerClosest.netId);

          var netid = enemy.GetComponent<NetworkIdentity>();
          netid.RemoveClientAuthority();
          netid.AssignClientAuthority(playerClosest.connectionToClient);
        }
      }
    }

  }

  //
  public static EnemyScript GetEnemy(Collider c)
  {
    var instanceId = c.gameObject.GetInstanceID();
    if (!s_EnemiesMapped.ContainsKey(instanceId)) return null;
    return s_EnemiesMapped[instanceId];
  }

  //
  public struct EnemySpawnData
  {
    public EnemyType EnemyType;
    public Vector3 Position;
  }

  [Server]
  public static EnemyScript SpawnEnemy(EnemySpawnData enemySpawnData)
  {
    var enemyScript = GameObject.Instantiate(NetworkManager.singleton.spawnPrefabs[0]).GetComponent<EnemyScript>();
    enemyScript.Init(enemySpawnData);
    NetworkServer.Spawn(enemyScript.gameObject);

    return enemyScript;
  }

  //
}
